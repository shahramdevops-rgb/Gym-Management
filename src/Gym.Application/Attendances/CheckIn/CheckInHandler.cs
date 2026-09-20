using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// Checks a member in: consumes a session from their current subscription and takes the
/// lowest-numbered free locker, if any (BUSINESS_RULES.md §7). Front desk work, so both roles.
/// </summary>
public sealed class CheckInHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure<AttendanceResponse>(MemberErrors.NotFound);
        }

        var canCheckIn = member.EnsureCanCheckIn();
        if (canCheckIn.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(canCheckIn.Error);
        }

        // One check-in per member at a time: the second waits here until the first commits, so
        // two requests cannot both consume the last session (the same reason SubscriptionSeller
        // locks the member before reading its subscriptions).
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(memberId, cancellationToken);

        var alreadyInside = await db.Attendances.AnyAsync(a => a.MemberId == memberId && a.CheckedOutAt == null, cancellationToken);
        if (alreadyInside)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.AlreadyCheckedIn);
        }

        var subscription = await db.Subscriptions
            .Where(s => s.MemberId == memberId && s.CancelledAt == null)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NoSubscription);
        }

        var consumed = subscription.ConsumeSession(calendar.Today());
        if (consumed.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(consumed.Error);
        }

        var freeLocker = await db.Lockers
            .Where(l => !l.IsOutOfService && !db.Attendances.Any(a => a.LockerId == l.Id && a.CheckedOutAt == null))
            .OrderBy(l => l.Number)
            .Select(l => new { l.Id, l.Number })
            .FirstOrDefaultAsync(cancellationToken);

        var attendance = Attendance.CheckIn(memberId, subscription.Id, freeLocker?.Id, time.GetUtcNow());
        db.Attendances.Add(attendance);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName is AttendanceConstraints.OneOpenPerMember or AttendanceConstraints.OneOpenPerLocker)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AttendanceResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        return AttendanceResponse.From(attendance, freeLocker?.Number);
    }
}

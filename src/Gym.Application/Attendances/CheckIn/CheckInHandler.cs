using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// Checks a member in: consumes a session from their current subscription and takes a random
/// free locker, if any (BUSINESS_RULES.md §7). Front desk work, so both roles.
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

        var today = calendar.Today();

        // Tracked, not AsNoTracking: InEffectToday may move the queue up, and those changes are
        // saved with the attendance below. Ones that ended before today cannot be used and cannot
        // be promoted, so they only matter for the error message.
        var live = await db.Subscriptions
            .Where(s => s.MemberId == memberId && s.CancelledAt == null)
            .ToListAsync(cancellationToken);

        if (live.Count == 0)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NoSubscription);
        }

        // The one usable today, not the one that ends last. A member who renewed early has a
        // queued subscription with a later end date, and taking that one would refuse them for
        // the rest of the term they already paid for.
        var subscription = SubscriptionSchedule.InEffectToday(today, live);
        if (subscription is null)
        {
            // Nothing is usable. Report why through the subscription the member is most likely
            // asking about — the one that ends last — and let the entity name the reason
            // (expired, frozen, exhausted, not started yet).
            var latest = live.MaxBy(s => s.EndDate)!;

            return Result.Failure<AttendanceResponse>(latest.ConsumeSession(today).Error);
        }

        var consumed = subscription.ConsumeSession(today);
        if (consumed.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(consumed.Error);
        }

        // Any free locker is equally correct, so the pick is random rather than by number
        // (BUSINESS_RULES.md §7). Taking the lowest number every time wore out the first few
        // lockers while the high numbers were never touched. `EF.Functions.Random()` becomes
        // Postgres's `random()`, so the ordering stays one query on the database side.
        var freeLocker = await db.Lockers
            .Where(l => !l.IsOutOfService && !db.Attendances.Any(a => a.LockerId == l.Id && a.CheckedOutAt == null))
            .OrderBy(l => EF.Functions.Random())
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

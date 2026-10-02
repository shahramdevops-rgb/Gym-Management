using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CardioOnlyCheckIn;

/// <summary>
/// Lets a member in only for هوازی (BUSINESS_RULES.md §7 <i>Cardio-only visit</i>): the member must
/// hold a membership that is active today or frozen, no session is consumed, and the plan is not
/// changed in any way. The visit holds the locker the desk chose or a reserve place, like any visit.
/// Front desk work, so both roles; the audit log records who let the member in this way.
/// </summary>
/// <remarks>
/// The same lock and checks as <c>CheckInHandler</c>, without the sale and without anything that
/// moves a plan (unfreezing, bringing a queued plan forward). Nothing in a subscription is written,
/// so there is no overlap check to defer.
/// </remarks>
public sealed class CardioOnlyCheckInHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid memberId, CardioOnlyCheckInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        // The same per-member lock as an ordinary check-in, so the two cannot both let the member in.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(memberId, cancellationToken);

        var alreadyInside = await db.Attendances.AnyAsync(a => a.MemberId == memberId && a.CheckedOutAt == null, cancellationToken);
        if (alreadyInside)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.AlreadyCheckedIn);
        }

        // AsNoTracking: the plan is only read. Nothing on it changes, not even its sessions.
        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(s => s.MemberId == memberId && s.CancelledAt == null)
            .ToListAsync(cancellationToken);

        var plan = SubscriptionSchedule.PlanForCardioOnly(calendar.Today(), subscriptions);
        if (plan.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(plan.Error);
        }

        var now = time.GetUtcNow();
        int? lockerNumber = null;
        Attendance attendance;
        if (command.LockerId is { } lockerId)
        {
            var locker = await LockerChoice.CheckAsync(db, lockerId, cancellationToken);
            if (locker.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(locker.Error);
            }

            lockerNumber = locker.Value;
            attendance = Attendance.CheckInCardioOnly(memberId, plan.Value.Id, lockerId, now);
        }
        else
        {
            var reserveSlot = await LockerChoice.FreeReserveSlotAsync(db, cancellationToken);
            if (reserveSlot.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(reserveSlot.Error);
            }

            attendance = Attendance.CheckInCardioOnlyOnReservePlace(memberId, plan.Value.Id, reserveSlot.Value, now);
        }

        db.Attendances.Add(attendance);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == AttendanceConstraints.OneOpenPerLocker)
        {
            // Another desk gave out the same locker a moment earlier: the same answer as check-in's.
            return Result.Failure<AttendanceResponse>(AttendanceErrors.LockerTaken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName is AttendanceConstraints.OneOpenPerMember or AttendanceConstraints.OneOpenPerReserveSlot)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }

        // Shown at the desk the same way as after an ordinary check-in (BUSINESS_RULES.md §7):
        // money owed never blocks coming in.
        var debt = await MemberDebt.GetTotalAsync(db, memberId, cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber, serviceCharges: [], debt);
    }
}

using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// Checks a member in: consumes a session from their current subscription and gives them the
/// locker the desk chose, or a reserve place when every locker is full (BUSINESS_RULES.md §6, §7).
/// Front desk work, so both roles.
/// </summary>
public sealed class CheckInHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid memberId, CheckInCommand command, CancellationToken cancellationToken)
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
            return Result.Failure<AttendanceResponse>(NothingUsableToday(today, live));
        }

        var consumed = subscription.ConsumeSession(today);
        if (consumed.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(consumed.Error);
        }

        // The place the desk chose, after the subscription: a member who cannot come in today hears
        // why (and is offered a single visit) whichever locker was clicked. Nothing is saved on a
        // failure here, so the session consumed above goes nowhere.
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
            attendance = Attendance.CheckIn(memberId, subscription.Id, lockerId, now);
        }
        else
        {
            var reserveSlot = await FreeReserveSlotAsync(cancellationToken);
            if (reserveSlot.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(reserveSlot.Error);
            }

            attendance = Attendance.CheckInOnReservePlace(memberId, subscription.Id, reserveSlot.Value, now);
        }

        db.Attendances.Add(attendance);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == AttendanceConstraints.OneOpenPerLocker)
        {
            // Another desk gave out the same locker a moment earlier. The desk's next step is the
            // same as for one taken long ago — choose another — so the error is too.
            return Result.Failure<AttendanceResponse>(AttendanceErrors.LockerTaken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName is AttendanceConstraints.OneOpenPerMember or AttendanceConstraints.OneOpenPerReserveSlot)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AttendanceResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        // BUSINESS_RULES.md §7: money owed never blocks a check-in. The visit is already committed;
        // this only tells the front desk what to mention while the member is still at the desk.
        // Read after the commit because check-in moves no money, so the total cannot have changed.
        var debt = await MemberDebt.GetTotalAsync(db, memberId, cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber, serviceCharges: [], debt);
    }

    /// <summary>
    /// The lowest reserve place not held by an open visit (BUSINESS_RULES.md §6). Which one does
    /// not matter — its number is never shown — so lowest is simply the easiest to reason about.
    /// </summary>
    /// <remarks>
    /// Two desks asking at once can both read the same place as free. The partial unique index on
    /// the place refuses the second, which then hears "try again", and the retry reads afresh.
    /// </remarks>
    private async Task<Result<int>> FreeReserveSlotAsync(CancellationToken cancellationToken)
    {
        if (await LockerChoice.AnyFreeAsync(db, cancellationToken))
        {
            return Result.Failure<int>(AttendanceErrors.LockersStillFree);
        }

        var held = await db.Attendances
            .Where(a => a.CheckedOutAt == null && a.ReserveSlot != null)
            .Select(a => a.ReserveSlot!.Value)
            .ToListAsync(cancellationToken);

        var free = Enumerable.Range(1, Attendance.ReservePlaceCount).Except(held).ToList();

        return free.Count == 0 ? Result.Failure<int>(AttendanceErrors.ReserveFull) : free[0];
    }

    /// <summary>
    /// Why nothing is usable today, said the way the front desk has to say it to the member.
    /// </summary>
    /// <remarks>
    /// An exhausted subscription with a renewal queued behind it is the case worth naming. The
    /// queue only moves up once the exhausted one stops covering today (BUSINESS_RULES.md §4), so
    /// a member who used every session on the day they bought the plan and renewed the same day
    /// waits until tomorrow. Reading the reason off the queued subscription alone would answer
    /// "it has not started yet", which sounds like the sale went wrong rather than "come back
    /// tomorrow".
    /// </remarks>
    private static Error NothingUsableToday(DateOnly today, List<Subscription> live)
    {
        // Memberships only, for the same reason the promotion itself reads memberships only: a used
        // single-session subscription is exhausted by design, and pairing it with a membership queued
        // for next week would answer "come back tomorrow" when the truth is "come back on Saturday".
        var exhausted = live.Exists(s => !s.IsSingleSession && s.GetStatus(today) == SubscriptionStatus.Exhausted);
        var queued = live.Exists(s => !s.IsSingleSession && s.GetStatus(today) == SubscriptionStatus.Upcoming);
        if (exhausted && queued)
        {
            return SubscriptionErrors.NextStartsTomorrow;
        }

        // Otherwise report through the subscription the member is most likely asking about — the
        // one that ends last — and let the entity name the reason (expired, frozen, exhausted,
        // not started yet).
        return live.MaxBy(s => s.EndDate)!.ConsumeSession(today).Error;
    }
}

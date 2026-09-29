using Gym.Application.Common;
using Gym.Application.Members;
using Gym.Application.Subscriptions;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// Checks a member in: consumes a session from their current subscription and gives them the
/// locker the desk chose, or a reserve place when every locker is full (BUSINESS_RULES.md §6, §7).
/// When the desk sells a single visit or a plan in the same box, the sale happens here too, in the
/// same transaction: both are saved or neither is (roadmap 6.5.7). A member whose plan is frozen
/// and who has nothing else usable today is unfrozen here too (roadmap 6.5.9). Front desk work, so
/// both roles.
/// </summary>
public sealed class CheckInHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, SubscriptionSeller seller, ISubscriptionPolicy policy)
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

        if (command.Sale is { } sale)
        {
            var sold = await SellAsync(member, sale, today, cancellationToken);
            if (sold.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(sold.Error);
            }
        }

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

        // Nothing usable, but a plan is frozen: coming in ends the freeze (BUSINESS_RULES.md §4
        // Freeze, roadmap 6.5.9). Not with a sale: the desk sold something so that it would be
        // used, and a plan sold behind a frozen one must not be queued by the unfreeze below.
        int? unfrozenDays = null;
        if (subscription is null && command.Sale is null && SubscriptionSchedule.FrozenToResume(live) is { } frozen)
        {
            // Unfreezing moves the queued plans behind this one, which passes through a moment
            // where their date ranges overlap, the same as the Owner's unfreeze.
            await db.DeferSubscriptionOverlapCheckAsync(cancellationToken);

            var unfrozen = SubscriptionSchedule.Unfreeze(frozen, today, policy.MaxFreezeDays, live);
            if (unfrozen.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(unfrozen.Error);
            }

            unfrozenDays = unfrozen.Value;
            // If it ran out while frozen, ConsumeSession below refuses with Expired and nothing is
            // saved, so the plan stays frozen for the Owner to decide about.
            subscription = frozen;
        }

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
            // Through the context, so an overlap check deferred by an unfreeze is translated too.
            await db.CommitTransactionAsync(transaction, cancellationToken);
        }
        catch (ExclusionConstraintException exception) when (exception.ConstraintName == SubscriptionConstraints.NoOverlap)
        {
            return Result.Failure<AttendanceResponse>(SubscriptionErrors.ChangedConcurrently);
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

        return AttendanceResponse.From(attendance, lockerNumber, serviceCharges: [], debt) with { UnfrozenDays = unfrozenDays };
    }

    /// <summary>
    /// Sells what the desk chose and saves it inside the check-in's transaction. Saved rather than
    /// only added, so the lookup that follows finds it the way it finds any other subscription and
    /// it carries its <c>CreatedAt</c>, which decides which of two single visits is used first.
    /// Nothing is committed until the visit is: if the member still cannot come in today (a plan
    /// queued behind a frozen one, say), or the locker is gone, the sale is rolled back with it.
    /// </summary>
    private async Task<Result> SellAsync(Member member, CheckInSale sale, DateOnly today, CancellationToken cancellationToken)
    {
        var added = sale.Kind == CheckInSaleKind.SingleVisit
            ? await seller.AddSingleVisitAsync(member, today, cancellationToken)
            // The validator has already required the session count for a plan.
            : await seller.AddMembershipAsync(member, sale.SessionCount!.Value, today, cancellationToken);

        if (added.IsFailure)
        {
            return Result.Failure(added.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ExclusionConstraintException exception) when (exception.ConstraintName == SubscriptionConstraints.NoOverlap)
        {
            return Result.Failure(SubscriptionErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(SubscriptionErrors.ChangedConcurrently);
        }

        return Result.Success();
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

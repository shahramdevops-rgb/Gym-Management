using Gym.Domain.Common;
using Gym.Domain.Plans;

namespace Gym.Domain.Subscriptions;

/// <summary>
/// A plan sold to a member (BUSINESS_RULES.md §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Snapshot.</b> The plan's name, price, duration and session count are copied at the sale.
/// Editing or deactivating the plan later changes nothing here, the way editing a price list
/// does not change a receipt already printed.
/// </para>
/// <para>
/// <b>Calculated status.</b> <see cref="GetStatus"/> works the status out from the dates, the
/// sessions and "today". Nothing stores it, so nothing has to update it at midnight.
/// </para>
/// <para>
/// <b>"Today" is a parameter.</b> Every rule that depends on the date takes a
/// <see cref="DateOnly"/> in the gym's time zone. The entity never reads a clock, so a test can
/// put it on any day, and the caller is the one place that decides what "today" means.
/// </para>
/// </remarks>
public sealed class Subscription : Entity
{
    public const int CancellationReasonMaxLength = 500;

    // For EF Core.
    private Subscription()
    {
    }

    public Guid MemberId { get; private set; }

    /// <summary>
    /// The plan it was sold from. Every rule reads the snapshot below instead, with one
    /// exception: the plan's <i>name</i> is read live through this id rather than copied, so
    /// renaming a plan corrects the label everywhere it has ever been sold (BUSINESS_RULES.md §4).
    /// The numbers stay snapshotted — they are the contract, the name is only how it reads.
    /// </summary>
    public Guid PlanId { get; private set; }

    public decimal Price { get; private set; }

    public int DurationDays { get; private set; }

    /// <summary><c>null</c> means unlimited sessions.</summary>
    public int? TotalSessions { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>
    /// Inclusive: the member may still come on this day. Starts as
    /// <c>StartDate + DurationDays - 1</c> and moves later when a freeze ends.
    /// </summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>Counted for unlimited plans too, for reports.</summary>
    public int UsedSessions { get; private set; }

    /// <summary>The day the current freeze began; <c>null</c> when not frozen.</summary>
    public DateOnly? FrozenSince { get; private set; }

    /// <summary>Freeze days already added to <see cref="EndDate"/>, over all past freezes.</summary>
    public int TotalFrozenDays { get; private set; }

    /// <summary>A moment (UTC), not a business date: it records when someone pressed the button.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>Postgres <c>xmin</c> (configured in task 4.2): two check-ins cannot both use the last session.</summary>
    public uint Version { get; private set; }

    public bool IsUnlimited => TotalSessions is null;

    /// <summary><c>null</c> for an unlimited plan.</summary>
    public int? RemainingSessions => TotalSessions - UsedSessions;

    /// <summary>
    /// Sells <paramref name="plan"/> to a member, starting on <paramref name="startDate"/>.
    /// Choosing that date (today, or queued after the member's latest subscription) needs the
    /// member's other subscriptions, so the caller does it (task 4.2).
    /// </summary>
    public static Result<Subscription> Create(Guid memberId, Plan plan, DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sellable = plan.EnsureCanBeSold();
        if (sellable.IsFailure)
        {
            return Result.Failure<Subscription>(sellable.Error);
        }

        return new Subscription
        {
            MemberId = memberId,
            PlanId = plan.Id,
            Price = plan.Price,
            DurationDays = plan.DurationDays,
            TotalSessions = plan.SessionCount,
            StartDate = startDate,
            // Inclusive end: a 30-day plan starting on the 1st ends on the 30th, not the 31st.
            EndDate = startDate.AddDays(plan.DurationDays - 1),
        };
    }

    /// <summary>BUSINESS_RULES.md §4: the first status that applies wins, in this order.</summary>
    public SubscriptionStatus GetStatus(DateOnly today)
    {
        if (CancelledAt is not null)
        {
            return SubscriptionStatus.Cancelled;
        }

        if (FrozenSince is not null)
        {
            return SubscriptionStatus.Frozen;
        }

        if (today < StartDate)
        {
            return SubscriptionStatus.Upcoming;
        }

        if (today > EndDate)
        {
            return SubscriptionStatus.Expired;
        }

        // An unlimited plan (TotalSessions null) is never exhausted.
        if (TotalSessions is { } total && UsedSessions >= total)
        {
            return SubscriptionStatus.Exhausted;
        }

        return SubscriptionStatus.Active;
    }

    /// <summary>One visit. Only an active subscription can be used.</summary>
    public Result ConsumeSession(DateOnly today)
    {
        var active = EnsureActive(today);
        if (active.IsFailure)
        {
            return active;
        }

        UsedSessions++;

        return Result.Success();
    }

    /// <summary>
    /// Gives back a session when a check-in is cancelled (BUSINESS_RULES.md §7). Never goes below
    /// zero, and does not look at the status: the visit being undone was valid when it happened.
    /// </summary>
    public void RestoreSession()
    {
        if (UsedSessions > 0)
        {
            UsedSessions--;
        }
    }

    /// <summary>
    /// Starts a freeze today. Only an active subscription can be frozen, and only while freeze
    /// days are left.
    /// </summary>
    /// <param name="maxFreezeDays"><c>Gym:MaxFreezeDaysPerSubscription</c>.</param>
    public Result Freeze(DateOnly today, int maxFreezeDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFreezeDays);

        var active = EnsureActive(today);
        if (active.IsFailure)
        {
            return active;
        }

        if (TotalFrozenDays >= maxFreezeDays)
        {
            return Result.Failure(SubscriptionErrors.FreezeLimitReached);
        }

        FrozenSince = today;

        return Result.Success();
    }

    /// <summary>
    /// Ends the freeze and moves <see cref="EndDate"/> later by the frozen days, capped at the
    /// days still allowed (decided in task 4.1): the Owner can always unfreeze, but a freeze
    /// longer than the allowance does not extend the subscription beyond it.
    /// </summary>
    /// <returns>
    /// The days <see cref="EndDate"/> moved. The member's queued subscriptions move by the same
    /// number (task 4.3).
    /// </returns>
    public Result<int> Unfreeze(DateOnly today, int maxFreezeDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFreezeDays);

        if (CancelledAt is not null)
        {
            return Result.Failure<int>(SubscriptionErrors.Cancelled);
        }

        if (FrozenSince is not { } frozenSince)
        {
            return Result.Failure<int>(SubscriptionErrors.NotFrozen);
        }

        // A date before the freeze began means the caller's clock is wrong, not the user's input.
        ArgumentOutOfRangeException.ThrowIfLessThan(today, frozenSince);

        var frozenDays = today.DayNumber - frozenSince.DayNumber;
        var allowedDays = Math.Max(0, maxFreezeDays - TotalFrozenDays);
        var addedDays = Math.Min(frozenDays, allowedDays);

        EndDate = EndDate.AddDays(addedDays);
        TotalFrozenDays += addedDays;
        FrozenSince = null;

        return addedDays;
    }

    /// <summary>
    /// Ends an exhausted subscription early so a new one can start today instead of waiting for
    /// this one's <see cref="EndDate"/> (decided in task 4.2). It ends yesterday, or today if it
    /// only started today, so the two never cover the same date.
    /// </summary>
    public void CloseExhaustedEarly(DateOnly today)
    {
        if (GetStatus(today) != SubscriptionStatus.Exhausted)
        {
            throw new InvalidOperationException("Only an exhausted subscription can be closed early.");
        }

        var yesterday = today.AddDays(-1);
        EndDate = yesterday < StartDate ? StartDate : yesterday;
    }

    /// <summary>
    /// Brings a queued subscription forward to <paramref name="newStart"/>, because the member's
    /// current subscription ran out of sessions before its own end date and there is no reason to
    /// keep them waiting (BUSINESS_RULES.md §4). The duration is preserved — the member still gets
    /// every day they paid for — so <see cref="EndDate"/> moves by the same amount.
    /// </summary>
    public void StartEarly(DateOnly newStart)
    {
        if (GetStatus(newStart) != SubscriptionStatus.Upcoming)
        {
            throw new InvalidOperationException("Only a queued subscription can be started early.");
        }

        StartDate = newStart;
        EndDate = newStart.AddDays(DurationDays - 1);
    }

    /// <summary>
    /// Moves a queued subscription later by <paramref name="days"/>, because unfreezing another
    /// subscription of the same member pushed its <c>EndDate</c> out by that many days
    /// (BUSINESS_RULES.md §4 Freeze, task 4.3). The caller picks which subscriptions are queued
    /// behind the one being unfrozen; this only knows how to move.
    /// </summary>
    public void ShiftQueued(int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        StartDate = StartDate.AddDays(days);
        EndDate = EndDate.AddDays(days);
    }

    /// <summary>
    /// Cancels a subscription nobody has used yet (BUSINESS_RULES.md §4 Cancel, decided
    /// 1405/06/31 — replaces the earlier task 4.1 rule that any status could be cancelled). A
    /// service that has been consumed is not un-sold, and an expired or exhausted subscription is
    /// history. Payments are untouched: money goes back only through refunds (§4, §5).
    /// </summary>
    public Result Cancel(string reason, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (CancelledAt is not null)
        {
            return Result.Failure(SubscriptionErrors.Cancelled);
        }

        if (UsedSessions > 0)
        {
            return Result.Failure(SubscriptionErrors.AlreadyUsed);
        }

        if (GetStatus(today) is not (SubscriptionStatus.Upcoming or SubscriptionStatus.Active or SubscriptionStatus.Frozen))
        {
            return Result.Failure(SubscriptionErrors.Expired);
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(SubscriptionErrors.CancelReasonRequired);
        }

        if (cleanReason.Length > CancellationReasonMaxLength)
        {
            return Result.Failure(SubscriptionErrors.CancelReasonTooLong);
        }

        CancelledAt = now;
        CancellationReason = cleanReason;

        return Result.Success();
    }

    /// <summary>Success when active; otherwise the error that says why not.</summary>
    private Result EnsureActive(DateOnly today) => GetStatus(today) switch
    {
        SubscriptionStatus.Active => Result.Success(),
        SubscriptionStatus.Cancelled => Result.Failure(SubscriptionErrors.Cancelled),
        SubscriptionStatus.Frozen => Result.Failure(SubscriptionErrors.Frozen),
        SubscriptionStatus.Upcoming => Result.Failure(SubscriptionErrors.NotStarted),
        SubscriptionStatus.Expired => Result.Failure(SubscriptionErrors.Expired),
        SubscriptionStatus.Exhausted => Result.Failure(SubscriptionErrors.NoSessionsLeft),
        var status => throw new InvalidOperationException($"Unknown subscription status {status}."),
    };
}

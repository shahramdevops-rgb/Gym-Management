using Gym.Domain.Attendances;
using Gym.Domain.Common;

namespace Gym.Domain.Subscriptions;

/// <summary>
/// Where a new subscription goes in a member's calendar (BUSINESS_RULES.md §4): a member never
/// has two subscriptions covering the same date.
/// </summary>
/// <remarks>
/// Single-session subscriptions are outside all of this on purpose (§4 <i>Single-session
/// subscriptions</i>). They are one day for one visit, so they neither wait for the member's calendar
/// nor change it: every method here reads memberships only, except the lookup for who may come in
/// today, which has to be able to find the single visit the member just bought.
/// </remarks>
public static class SubscriptionSchedule
{
    /// <summary>
    /// The start date for a new membership: today when nothing current or queued remains,
    /// otherwise the day after the latest end date. Cancelled subscriptions cover no dates, and
    /// neither do single-session ones as far as this rule is concerned.
    /// </summary>
    /// <remarks>
    /// If the latest membership is exhausted, it is closed early first (see
    /// <see cref="Subscription.CloseExhaustedEarly"/>), which changes that entity: the caller
    /// saves it together with the new subscription. A single-session subscription is never closed
    /// early and never pushes a membership's start date out — otherwise a member who dropped in
    /// today would have the plan they bought an hour later start tomorrow.
    /// </remarks>
    /// <param name="memberSubscriptions">All of one member's subscriptions, tracked for saving.</param>
    public static DateOnly NextStartDate(DateOnly today, IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        var live = Memberships(memberSubscriptions);
        if (live.Count == 0)
        {
            return today;
        }

        var latest = live.MaxBy(subscription => subscription.EndDate)!;
        if (latest.GetStatus(today) == SubscriptionStatus.Exhausted)
        {
            latest.CloseExhaustedEarly(today);
        }

        var lastEndDate = latest.EndDate;

        return lastEndDate < today ? today : lastEndDate.AddDays(1);
    }

    /// <summary>
    /// The subscription a member can actually use today, or <c>null</c> when none of them is
    /// usable (BUSINESS_RULES.md §4 and §7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <c>Active</c> subscription always wins, even when a later-starting queued renewal
    /// exists. Picking the latest end date instead — which is what renew does, and correctly —
    /// would hand back the queued one and refuse the member for the rest of their paid term.
    /// </para>
    /// <para>
    /// Among active subscriptions a single-session one is used first. It can only ever be used
    /// today and is worth nothing tomorrow, while the membership's sessions keep — so spending the
    /// visit the member just paid for is the answer in their favour. This only arises because a
    /// single visit may overlap a membership (§4). *Decided by Claude during task 6.5.3; pending
    /// review.*
    /// </para>
    /// <para>
    /// When nothing is active but the current membership is <c>Exhausted</c> with a renewal
    /// queued behind it, the queue moves up: the exhausted one is closed early and the soonest
    /// queued one starts today. This is the same principle <see cref="NextStartDate"/> applies at
    /// the moment of sale — an exhausted subscription should not make anyone wait — extended to
    /// the case where the sale happened first and the sessions ran out afterwards. A used
    /// single-session subscription is never the "exhausted" one here: it is exhausted by design,
    /// and letting it trigger the promotion would drag a membership queued for next week into
    /// starting today.
    /// </para>
    /// <para>
    /// Like <see cref="NextStartDate"/>, this changes the entities it is given and the caller
    /// saves them. It mutates nothing when it returns the active subscription or <c>null</c>.
    /// </para>
    /// </remarks>
    /// <param name="memberSubscriptions">All of one member's subscriptions, tracked for saving.</param>
    public static Subscription? InEffectToday(DateOnly today, IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        var live = memberSubscriptions.Where(subscription => subscription.CancelledAt is null).ToList();

        var active = live
            .Where(subscription => subscription.GetStatus(today) == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.IsSingleSession)
            .ThenBy(subscription => subscription.CreatedAt)
            .FirstOrDefault();

        if (active is not null)
        {
            return active;
        }

        var memberships = live.Where(subscription => !subscription.IsSingleSession).ToList();

        var exhausted = memberships.Find(subscription => subscription.GetStatus(today) == SubscriptionStatus.Exhausted);
        var queued = memberships
            .Where(subscription => subscription.GetStatus(today) == SubscriptionStatus.Upcoming)
            .MinBy(subscription => subscription.StartDate);

        if (exhausted is null || queued is null)
        {
            return null;
        }

        exhausted.CloseExhaustedEarly(today);

        // An exhausted subscription that only started today still covers today, so the queued one
        // cannot move onto it — two subscriptions never share a date. The member waits until
        // tomorrow, which is the same answer NextStartDate gives for a sale on that day.
        if (exhausted.EndDate >= today)
        {
            return null;
        }

        queued.StartEarly(today);

        return queued;
    }

    /// <summary>
    /// The frozen membership a check-in unfreezes when the member comes in (BUSINESS_RULES.md §4
    /// <i>Freeze</i>, roadmap 6.5.9), or <c>null</c> when none is frozen.
    /// </summary>
    /// <remarks>
    /// Asked only after <see cref="InEffectToday"/> found nothing: anything usable today — a single
    /// visit, another membership — is used first and the freeze is left alone. Single visits cannot
    /// be frozen, so only memberships are read. Two frozen at once is possible only when a queued
    /// plan was frozen after it started; the one that started first is the member's current term.
    /// </remarks>
    public static Subscription? FrozenToResume(IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        return Memberships(memberSubscriptions)
            .Where(subscription => subscription.FrozenSince is not null)
            .MinBy(subscription => subscription.StartDate);
    }

    /// <summary>
    /// Ends <paramref name="frozen"/>'s freeze and shifts the member's queued memberships by the days
    /// its <c>EndDate</c> moved (BUSINESS_RULES.md §4 <i>Freeze</i>, task 4.3). The one rule behind
    /// both the Owner's unfreeze and a check-in that ends a freeze (roadmap 6.5.9).
    /// </summary>
    /// <remarks>
    /// Queued means starting after the frozen one's end date as it was before the freeze ended. Single
    /// visits are never shifted: they are dated the day they were sold, and moving them would rewrite
    /// history (§4 <i>Single-session subscriptions</i>). The ranges pass through a moment where they
    /// overlap, so the caller defers the database's overlap check before saving.
    /// </remarks>
    /// <param name="memberSubscriptions">All of the member's subscriptions, tracked for saving.</param>
    /// <returns>The days <c>EndDate</c> moved, the same as <see cref="Subscription.Unfreeze"/>.</returns>
    public static Result<int> Unfreeze(
        Subscription frozen, DateOnly today, int maxFreezeDays, IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        var originalEndDate = frozen.EndDate;

        var unfrozen = frozen.Unfreeze(today, maxFreezeDays);
        if (unfrozen.IsFailure || unfrozen.Value == 0)
        {
            return unfrozen;
        }

        var queued = Memberships(memberSubscriptions)
            .Where(subscription => subscription.Id != frozen.Id && subscription.StartDate > originalEndDate);

        foreach (var subscription in queued)
        {
            subscription.ShiftQueued(unfrozen.Value);
        }

        return unfrozen;
    }

    /// <summary>
    /// The member's subscriptions that take part in the calendar: not cancelled, and not a single
    /// visit. Cancelled ones cover no dates; single visits are outside the ordering entirely.
    /// </summary>
    /// <summary>
    /// The plan a cardio-only visit is let in on (BUSINESS_RULES.md §7 <i>Cardio-only visit</i>): the
    /// membership active today, or else a frozen one. Nothing is changed: no session is consumed, a
    /// freeze is not ended and a queued plan is not moved forward, so unlike
    /// <see cref="InEffectToday"/> this mutates none of the entities it reads.
    /// </summary>
    /// <returns>
    /// The plan, or the reason the member cannot come in: <c>Attendance.NoSubscription</c> with no
    /// membership at all, otherwise what the membership that ends last says about today (expired,
    /// no sessions left, not started yet), the same reason an ordinary check-in would give.
    /// </returns>
    public static Result<Subscription> PlanForCardioOnly(DateOnly today, IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        var memberships = Memberships(memberSubscriptions);

        // Two memberships never cover the same date, so at most one is active.
        var active = memberships.Find(subscription => subscription.GetStatus(today) == SubscriptionStatus.Active);
        if (active is not null)
        {
            return active;
        }

        if (FrozenToResume(memberships) is { } frozen)
        {
            return frozen;
        }

        if (memberships.Count == 0)
        {
            return Result.Failure<Subscription>(AttendanceErrors.NoSubscription);
        }

        return Result.Failure<Subscription>(memberships.MaxBy(subscription => subscription.EndDate)!.EnsureActive(today).Error);
    }

    private static List<Subscription> Memberships(IEnumerable<Subscription> memberSubscriptions) =>
        memberSubscriptions
            .Where(subscription => subscription.CancelledAt is null && !subscription.IsSingleSession)
            .ToList();
}

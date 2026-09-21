namespace Gym.Domain.Subscriptions;

/// <summary>
/// Where a new subscription goes in a member's calendar (BUSINESS_RULES.md §4): a member never
/// has two subscriptions covering the same date.
/// </summary>
public static class SubscriptionSchedule
{
    /// <summary>
    /// The start date for a new subscription: today when nothing current or queued remains,
    /// otherwise the day after the latest end date. Cancelled subscriptions cover no dates.
    /// </summary>
    /// <remarks>
    /// If the latest subscription is exhausted, it is closed early first (see
    /// <see cref="Subscription.CloseExhaustedEarly"/>), which changes that entity: the caller
    /// saves it together with the new subscription.
    /// </remarks>
    /// <param name="memberSubscriptions">All of one member's subscriptions, tracked for saving.</param>
    public static DateOnly NextStartDate(DateOnly today, IEnumerable<Subscription> memberSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(memberSubscriptions);

        var live = memberSubscriptions.Where(subscription => subscription.CancelledAt is null).ToList();
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
    /// When nothing is active but the current subscription is <c>Exhausted</c> with a renewal
    /// queued behind it, the queue moves up: the exhausted one is closed early and the soonest
    /// queued one starts today. This is the same principle <see cref="NextStartDate"/> applies at
    /// the moment of sale — an exhausted subscription should not make anyone wait — extended to
    /// the case where the sale happened first and the sessions ran out afterwards.
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

        var active = live.Find(subscription => subscription.GetStatus(today) == SubscriptionStatus.Active);
        if (active is not null)
        {
            return active;
        }

        var exhausted = live.Find(subscription => subscription.GetStatus(today) == SubscriptionStatus.Exhausted);
        var queued = live
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
}

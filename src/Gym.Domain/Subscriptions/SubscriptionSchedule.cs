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
}

namespace Gym.Domain.Subscriptions;

/// <summary>
/// Calculated from a subscription and a date, never stored (BUSINESS_RULES.md §4). A stored
/// status would be wrong the morning after a subscription expires, until something rewrote it.
/// </summary>
/// <remarks>Declared in precedence order: when several apply, the first one wins.</remarks>
public enum SubscriptionStatus
{
    Cancelled,
    Frozen,
    Upcoming,
    Expired,
    Exhausted,
    Active,
}

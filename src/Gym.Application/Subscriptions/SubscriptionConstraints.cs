namespace Gym.Application.Subscriptions;

/// <summary>
/// Database constraint names the subscription handlers react to, shared with the migration that
/// creates them.
/// </summary>
public static class SubscriptionConstraints
{
    /// <summary>A member's non-cancelled subscriptions never cover the same date (BUSINESS_RULES.md §4).</summary>
    public const string NoOverlap = "ex_subscriptions_no_overlap";
}

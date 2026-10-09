namespace Gym.Application.Notifications;

/// <summary>
/// Names of the database constraints on <c>notifications</c>, so a caller can tell "already sent"
/// apart from any other failure, and the tests can name the constraint they expect.
/// </summary>
public static class NotificationConstraints
{
    /// <summary>One <c>SubscriptionExpiring</c> and one <c>LowSessions</c> per subscription (BUSINESS_RULES.md §10).</summary>
    public const string OnePerSubscriptionAndKind = "ix_notifications_one_per_subscription_and_kind";

    /// <summary>One <c>Birthday</c> per member and Jalali year (BUSINESS_RULES.md §10).</summary>
    public const string OneBirthdayPerMemberAndYear = "ix_notifications_one_birthday_per_member_and_year";

    /// <summary>One <c>PayableDue</c> per cheque or instalment (BUSINESS_RULES.md §10).</summary>
    public const string OnePerPayable = "ix_notifications_one_per_payable";

    /// <summary>Each kind fills the ids of its own event and leaves the others empty.</summary>
    public const string EventMatchesKind = "ck_notifications_event_matches_kind";

    /// <summary>A sent message has the provider's id and the moment it was sent.</summary>
    public const string SentHasProviderId = "ck_notifications_sent_has_provider_id";

    /// <summary>Only a sent message has a delivery.</summary>
    public const string DeliveryOnlyWhenSent = "ck_notifications_delivery_only_when_sent";

    /// <summary>A blocked or cancelled message costs nothing: the provider gave the cost back (BUSINESS_RULES.md §10).</summary>
    public const string RefundedHasNoCost = "ck_notifications_refunded_has_no_cost";
}

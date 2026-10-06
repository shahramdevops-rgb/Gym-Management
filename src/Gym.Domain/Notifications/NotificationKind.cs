namespace Gym.Domain.Notifications;

/// <summary>The four SMS the gym sends (BUSINESS_RULES.md §10 <i>The four kinds</i>).</summary>
public enum NotificationKind
{
    /// <summary>To the member: an active subscription's end date is near.</summary>
    SubscriptionExpiring,

    /// <summary>To the member: an active subscription has few sessions left.</summary>
    LowSessions,

    /// <summary>To the member: their birthday, by the Jalali month and day.</summary>
    Birthday,

    /// <summary>To the Owner: a pending cheque or instalment is coming due.</summary>
    PayableDue,
}

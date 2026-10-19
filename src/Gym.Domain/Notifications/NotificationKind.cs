namespace Gym.Domain.Notifications;

/// <summary>The SMS the gym sends (BUSINESS_RULES.md §10 <i>The four kinds</i>, <i>The daily report</i>, <i>The subscription receipt</i>).</summary>
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

    /// <summary>To the Owner: the day's check-ins, sales and money, every evening at 22:45 (task 10.7).</summary>
    DailyReport,

    /// <summary>To the member: their plan is fully paid (task 10.9), sent at once, not by a daily run.</summary>
    SubscriptionReceipt,
}

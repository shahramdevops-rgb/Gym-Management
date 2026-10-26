namespace Gym.Domain.Notifications;

/// <summary>The SMS the gym sends (BUSINESS_RULES.md §10 <i>The four kinds</i>, <i>The daily report</i>, <i>The subscription receipt</i>, <i>The welcome</i>, <i>The debt reminder</i>, <i>The absence SMS</i>, <i>The welcome back</i>, <i>The broadcast</i>).</summary>
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

    /// <summary>To the member: their first plan was sold (task 10.10), sent at once, once per member.</summary>
    Welcome,

    /// <summary>To the member: their debt has been owed for a while (task 10.11), once per debt, by a daily run.</summary>
    DebtReminder,

    /// <summary>To the member: they have a usable plan and have not come for a while (task 10.12), once per absence, by a daily run.</summary>
    Absence,

    /// <summary>To the member: the Owner's own text to a group of members (task 10.13), sent at once from its page.</summary>
    Broadcast,

    /// <summary>To the member: their first visit after an absence SMS (task 10.20), half an hour after the check-in, once per absence.</summary>
    WelcomeBack,
}

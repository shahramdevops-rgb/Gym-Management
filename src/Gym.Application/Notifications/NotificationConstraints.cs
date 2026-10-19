namespace Gym.Application.Notifications;

/// <summary>
/// Names of the database constraints on <c>notifications</c>, so a caller can tell "already sent"
/// apart from any other failure, and the tests can name the constraint they expect.
/// </summary>
public static class NotificationConstraints
{
    /// <summary>
    /// One <c>SubscriptionExpiring</c>, one <c>LowSessions</c>, one <c>SubscriptionReceipt</c> and one
    /// <c>Welcome</c> per subscription (BUSINESS_RULES.md §10).
    /// </summary>
    public const string OnePerSubscriptionAndKind = "ix_notifications_one_per_subscription_and_kind";

    /// <summary>One <c>Birthday</c> per member and Jalali year (BUSINESS_RULES.md §10).</summary>
    public const string OneBirthdayPerMemberAndYear = "ix_notifications_one_birthday_per_member_and_year";

    /// <summary>One <c>PayableDue</c> per cheque or instalment (BUSINESS_RULES.md §10).</summary>
    public const string OnePerPayable = "ix_notifications_one_per_payable";

    public const string OneDailyReportPerDay = "ix_notifications_one_daily_report_per_day";

    /// <summary>One <c>Welcome</c> per member, whichever plan was their first (BUSINESS_RULES.md §10).</summary>
    public const string OneWelcomePerMember = "ix_notifications_one_welcome_per_member";

    /// <summary>
    /// One <c>DebtReminder</c> per member and debt, the debt named by the day of its oldest unpaid sale
    /// (BUSINESS_RULES.md §10 <i>The debt reminder</i>).
    /// </summary>
    public const string OneDebtReminderPerMemberAndDebt = "ix_notifications_one_debt_reminder_per_member_and_debt";

    /// <summary>
    /// One <c>Absence</c> per member and absence, the absence named by the day it counts from
    /// (BUSINESS_RULES.md §10 <i>The absence SMS</i>).
    /// </summary>
    public const string OneAbsencePerMemberAndAbsence = "ix_notifications_one_absence_per_member_and_absence";

    /// <summary>Each kind fills the ids of its own event and leaves the others empty.</summary>
    public const string EventMatchesKind = "ck_notifications_event_matches_kind";

    /// <summary>A sent message has the provider's id and the moment it was sent.</summary>
    public const string SentHasProviderId = "ck_notifications_sent_has_provider_id";

    /// <summary>Only a sent message has a delivery.</summary>
    public const string DeliveryOnlyWhenSent = "ck_notifications_delivery_only_when_sent";

    /// <summary>A blocked or cancelled message costs nothing: the provider gave the cost back (BUSINESS_RULES.md §10).</summary>
    public const string RefundedHasNoCost = "ck_notifications_refunded_has_no_cost";
}

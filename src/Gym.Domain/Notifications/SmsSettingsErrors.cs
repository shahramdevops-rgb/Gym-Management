using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>The SMS settings page's refusals (BUSINESS_RULES.md §10 <i>SMS settings</i>).</summary>
public static class SmsSettingsErrors
{
    public static readonly Error SubscriptionExpiringDaysOutOfRange = Error.Validation(
        "Sms.SubscriptionExpiringDaysOutOfRange",
        "Days before the end date must be from 1 to 30.");

    public static readonly Error LowSessionsOutOfRange = Error.Validation(
        "Sms.LowSessionsOutOfRange",
        "Sessions left must be from 1 to 4.");

    /// <summary>«جلسات رو به اتمام» (task 10.17): the first row's largest plan, 5 to 29 (task 6.5.38).</summary>
    public static readonly Error LowSessionsSmallPlanMaxOutOfRange = Error.Validation(
        "Sms.LowSessionsSmallPlanMaxOutOfRange",
        $"The first row's largest plan must be from {LowSessionsSettings.SmallPlanMaxFloor} to {LowSessionsSettings.SmallPlanMaxCeiling} sessions.");

    /// <summary>The second row's largest plan: above the first row's, at most 30.</summary>
    public static readonly Error LowSessionsMediumPlanMaxOutOfRange = Error.Validation(
        "Sms.LowSessionsMediumPlanMaxOutOfRange",
        $"The second row's largest plan must be above the first row's and at most {LowSessionsSettings.MediumPlanMaxCeiling} sessions.");

    public static readonly Error BirthdayDaysOutOfRange = Error.Validation(
        "Sms.BirthdayDaysOutOfRange",
        "Days before the birthday must be from 1 to 7.");

    public static readonly Error PayableDueDaysOutOfRange = Error.Validation(
        "Sms.PayableDueDaysOutOfRange",
        "Days before the cheque's or instalment's date must be from 0 to 30.");

    public static readonly Error DebtReminderDaysOutOfRange = Error.Validation(
        "Sms.DebtReminderDaysOutOfRange",
        "Days since the oldest unpaid sale must be from 1 to 90.");

    public static readonly Error AbsenceDaysOutOfRange = Error.Validation(
        "Sms.AbsenceDaysOutOfRange",
        "Days away must be from 7 to 60.");

    /// <summary>The debt reminder's minimum amount (task 10.11): 1,000 Toman or more, at most 2 decimal places.</summary>
    public static readonly Error DebtReminderMinAmountOutOfRange = Error.Validation(
        "Sms.DebtReminderMinAmountOutOfRange",
        "The minimum amount must be at least 1,000 Toman, with at most 2 decimal places.");

    public static readonly Error SendTimeOutOfRange = Error.Validation(
        "Sms.SendTimeOutOfRange",
        "The send time must be between 08:00 and 22:00, on a quarter hour.");

    /// <summary>The daily report's time (task 10.17): 08:00 to 23:45, on a quarter hour.</summary>
    public static readonly Error DailyReportSendTimeOutOfRange = Error.Validation(
        "Sms.DailyReportSendTimeOutOfRange",
        "The daily report's time must be between 08:00 and 23:45, on a quarter hour.");

    /// <summary>A kind was turned on while one of its fields is empty (§10: on means filled).</summary>
    public static readonly Error SettingsIncomplete = Error.Validation(
        "Sms.SettingsIncomplete",
        "A kind can only be turned on when all of its fields are filled.");

    /// <summary>
    /// The daily report's number is not an Iranian mobile (§2's phone rules). Its own code, so the page
    /// puts it under that number and not under the cheque reminder's.
    /// </summary>
    public static readonly Error DailyReportPhoneInvalid = Error.Validation(
        "Sms.DailyReportPhoneInvalid",
        "The daily report's number must be an Iranian mobile number.");

    /// <summary>Two people saved the settings at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Sms.ChangedConcurrently",
        "The SMS settings were changed by someone else at the same moment. Reload and try again.");
}

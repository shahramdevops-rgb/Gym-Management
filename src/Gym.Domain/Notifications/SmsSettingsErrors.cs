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
        "Sessions left must be from 1 to 10.");

    public static readonly Error BirthdayDaysOutOfRange = Error.Validation(
        "Sms.BirthdayDaysOutOfRange",
        "Days before the birthday must be from 0 to 7.");

    public static readonly Error PayableDueDaysOutOfRange = Error.Validation(
        "Sms.PayableDueDaysOutOfRange",
        "Days before the cheque's or instalment's date must be from 0 to 30.");

    public static readonly Error SendTimeOutOfRange = Error.Validation(
        "Sms.SendTimeOutOfRange",
        "The send time must be between 08:00 and 22:00, on a quarter hour.");

    /// <summary>A kind was turned on while one of its fields is empty (§10: on means filled).</summary>
    public static readonly Error SettingsIncomplete = Error.Validation(
        "Sms.SettingsIncomplete",
        "A kind can only be turned on when all of its fields are filled.");

    /// <summary>Two people saved the settings at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Sms.ChangedConcurrently",
        "The SMS settings were changed by someone else at the same moment. Reload and try again.");
}

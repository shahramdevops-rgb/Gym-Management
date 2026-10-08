using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Notifications.NotFound",
        "No SMS has that id.");

    public static readonly Error NotPending = Error.Conflict(
        "Notifications.NotPending",
        "The message is no longer waiting to be sent.");

    public static readonly Error NotSent = Error.Conflict(
        "Notifications.NotSent",
        "Only a sent message has a delivery.");

    /// <summary>
    /// BUSINESS_RULES.md §10 <i>Sending</i>: only a <c>Failed</c> or <c>Unknown</c> message is resent.
    /// A sent one would be paid for twice, and a pending one is being sent right now.
    /// </summary>
    public static readonly Error NotResendable = Error.Conflict(
        "Notifications.NotResendable",
        "Only a failed message, or one whose outcome is unknown, can be resent.");

    /// <summary>BUSINESS_RULES.md §10 <i>Sending</i>: nothing is sent outside 08:00–22:00, a resend included.</summary>
    public static readonly Error OutsideSendingHours = Error.BusinessRule(
        "Notifications.OutsideSendingHours",
        "No SMS is sent outside 08:00–22:00.");

    /// <summary>The history's filter: the first day is after the last.</summary>
    public static readonly Error InvalidDateRange = Error.Validation(
        "Notifications.InvalidDateRange",
        "The start date must be on or before the end date.");

    /// <summary>Another resend, or a run, changed the message at the same moment.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Notifications.ChangedConcurrently",
        "The message was changed at the same moment. Reload the list and try again.");
}

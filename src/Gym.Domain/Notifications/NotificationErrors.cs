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
    /// BUSINESS_RULES.md §10 <i>Sending</i>: once the provider has given a message's cost back (blocked or
    /// cancelled), the cost is 0 and the delivery is final.
    /// </summary>
    public static readonly Error DeliveryRefunded = Error.Conflict(
        "Notifications.DeliveryRefunded",
        "The provider gave this message's cost back; its delivery no longer changes.");

    /// <summary>
    /// BUSINESS_RULES.md §10 <i>Sending</i>: only a <c>Failed</c> or <c>Unknown</c> message is resent, or
    /// a sent one the provider cancelled (task 10.16). Any other sent one would be paid for twice, and a
    /// pending one is being sent right now.
    /// </summary>
    public static readonly Error NotResendable = Error.Conflict(
        "Notifications.NotResendable",
        "Only a failed message, one whose outcome is unknown, or one the provider cancelled can be resent.");

    /// <summary>BUSINESS_RULES.md §10 <i>Sending</i> (task 10.16): the system resends by itself only what the provider cancelled.</summary>
    public static readonly Error NotCancelledByProvider = Error.Conflict(
        "Notifications.NotCancelledByProvider",
        "Only a message the provider cancelled is resent automatically.");

    /// <summary>BUSINESS_RULES.md §10 <i>Sending</i> (task 10.16): at most five automatic resends; then only the Owner's.</summary>
    public static readonly Error AutoResendsUsedUp = Error.Conflict(
        "Notifications.AutoResendsUsedUp",
        "The message was already resent automatically as many times as allowed.");

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

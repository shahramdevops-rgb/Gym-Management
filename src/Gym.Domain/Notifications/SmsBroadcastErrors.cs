using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>The broadcast page's refusals (BUSINESS_RULES.md §10 <i>The broadcast</i>).</summary>
public static class SmsBroadcastErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sms.BroadcastNotFound",
        "No broadcast has that id.");

    public static readonly Error TextRequired = Error.Validation(
        "Sms.BroadcastTextRequired",
        "Write the text of the broadcast.");

    /// <summary>Some recipient's message would take more than <see cref="SmsBroadcast.MaxParts"/> SMS parts.</summary>
    public static readonly Error TooLong = Error.Validation(
        "Sms.BroadcastTooLong",
        "The text is too long: with the longest first name, a message must fit in 3 SMS parts.");

    public static readonly Error UnknownAudience = Error.Validation(
        "Sms.BroadcastUnknownAudience",
        "Choose who the broadcast goes to.");

    /// <summary>All SMS or the broadcast's own switch is off on the settings page.</summary>
    public static readonly Error Off = Error.BusinessRule(
        "Sms.BroadcastOff",
        "Turn on all SMS and the broadcast on the SMS settings page first.");

    /// <summary>The chosen group has nobody in it now: nothing would be sent.</summary>
    public static readonly Error NoRecipients = Error.BusinessRule(
        "Sms.BroadcastNoRecipients",
        "Nobody is in the chosen group.");

    /// <summary>Another broadcast is still sending; the database holds one at a time too.</summary>
    public static readonly Error InProgress = Error.Conflict(
        "Sms.BroadcastInProgress",
        "Another broadcast is still being sent. Wait until it ends.");

    /// <summary>Only a stopped broadcast can be continued.</summary>
    public static readonly Error NotStopped = Error.Conflict(
        "Sms.BroadcastNotStopped",
        "Only a stopped broadcast can be continued.");

    /// <summary>Only a broadcast still sending can finish or stop: its job ran twice.</summary>
    public static readonly Error NotSending = Error.Conflict(
        "Sms.BroadcastNotSending",
        "The broadcast is no longer being sent.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Sms.BroadcastChangedConcurrently",
        "The broadcast was changed at the same moment. Reload the page and try again.");
}

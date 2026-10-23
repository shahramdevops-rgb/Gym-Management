namespace Gym.Application.Notifications.SendSubscriptionReceipt;

/// <summary>What became of one receipt job, for the log and the tests.</summary>
public enum ReceiptOutcome
{
    Sent,

    /// <summary>Refused, the credit used up, or every try used: the Owner can resend it.</summary>
    Failed,

    /// <summary>The request left and no answer came: it may have arrived.</summary>
    Unknown,

    /// <summary>A job stopped mid-send left the row <c>Pending</c>; it is now <c>Unknown</c>.</summary>
    Interrupted,

    /// <summary>The subscription already has its receipt.</summary>
    AlreadyWritten,

    /// <summary>All SMS or the receipt has been turned off since the payment.</summary>
    Off,

    /// <summary>Outside 08:00–22:00: put off until 10:00.</summary>
    Deferred,

    /// <summary>Cancelled, no longer fully paid, or the member deactivated since the payment.</summary>
    NoLongerDue,
}

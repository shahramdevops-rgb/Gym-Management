namespace Gym.Domain.Notifications;

/// <summary>
/// Whether a sent SMS reached the phone, as the provider reports it after sending
/// (BUSINESS_RULES.md §10 <i>Sending</i>). A sent message whose delivery is not known yet has none.
/// </summary>
public enum SmsDelivery
{
    Delivered,

    /// <summary>The phone was off or out of reach, or the carrier failed: it may still arrive later.</summary>
    NotDelivered,

    /// <summary>The receiver has blocked messages from this sender. The provider gives the cost back.</summary>
    BlockedByReceiver,

    /// <summary>The provider cancelled it (a sending error on its side) and gave the cost back.</summary>
    Cancelled,
}

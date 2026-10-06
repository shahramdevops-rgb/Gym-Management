namespace Gym.Domain.Notifications;

/// <summary>
/// Whether a sent SMS reached the phone, as the provider reports it after sending
/// (BUSINESS_RULES.md §10 <i>Sending</i>). A sent message whose delivery is not known yet has none.
/// </summary>
public enum SmsDelivery
{
    Delivered,

    NotDelivered,

    /// <summary>The receiver has blocked messages from this sender.</summary>
    BlockedByReceiver,
}

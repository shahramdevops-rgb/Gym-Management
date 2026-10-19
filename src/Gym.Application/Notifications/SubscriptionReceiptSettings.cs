namespace Gym.Application.Notifications;

/// <summary>
/// The receipt's row on the settings page as the page sends it (BUSINESS_RULES.md §10 <i>The
/// subscription receipt</i>): only on or off. It goes to the member's own number, at the payment.
/// </summary>
public sealed record SubscriptionReceiptSettings(bool Enabled);

/// <param name="MorningSendTime">10:00, fixed: when a receipt for a payment made after 22:00 goes.</param>
public sealed record SubscriptionReceiptSettingsResponse(bool Enabled, TimeOnly MorningSendTime);

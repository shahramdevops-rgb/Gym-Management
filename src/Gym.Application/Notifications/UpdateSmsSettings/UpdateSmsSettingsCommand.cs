using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.UpdateSmsSettings;

/// <param name="OwnerPhone">As typed (any digits, <c>09…</c> or <c>+98…</c>); empty clears it.</param>
/// <param name="Version">The <c>version</c> from the settings as they were read before editing.</param>
public sealed record UpdateSmsSettingsCommand(
    bool Enabled,
    SmsKindSettings SubscriptionExpiring,
    SmsKindSettings LowSessions,
    SmsKindSettings Birthday,
    SmsKindSettings PayableDue,
    string? OwnerPhone,
    uint Version);

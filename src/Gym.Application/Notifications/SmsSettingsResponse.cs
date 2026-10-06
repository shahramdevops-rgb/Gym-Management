using Gym.Domain.Notifications;

namespace Gym.Application.Notifications;

/// <param name="Enabled">The switch for every SMS.</param>
/// <param name="OwnerPhone">E.164 (<c>+989…</c>), or <c>null</c> until the Owner enters it.</param>
/// <param name="Version">Sent back with an edit, so an edit made on stale settings is refused.</param>
public sealed record SmsSettingsResponse(
    bool Enabled,
    SmsKindSettings SubscriptionExpiring,
    SmsKindSettings LowSessions,
    SmsKindSettings Birthday,
    SmsKindSettings PayableDue,
    string? OwnerPhone,
    uint Version)
{
    public static SmsSettingsResponse From(SmsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SmsSettingsResponse(
            settings.Enabled,
            settings.For(NotificationKind.SubscriptionExpiring),
            settings.For(NotificationKind.LowSessions),
            settings.For(NotificationKind.Birthday),
            settings.For(NotificationKind.PayableDue),
            settings.OwnerPhone,
            settings.Version);
    }
}

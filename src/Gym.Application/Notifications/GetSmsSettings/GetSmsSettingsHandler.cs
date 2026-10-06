using Gym.Application.Common;

namespace Gym.Application.Notifications.GetSmsSettings;

/// <summary>The SMS settings page as it is saved (BUSINESS_RULES.md §10 <i>SMS settings</i>). Owner only.</summary>
public sealed class GetSmsSettingsHandler(IAppDbContext db)
{
    public async Task<SmsSettingsResponse> Handle(CancellationToken cancellationToken) =>
        SmsSettingsResponse.From(await SmsSettingsRow.CurrentAsync(db, cancellationToken));
}

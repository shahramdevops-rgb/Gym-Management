using Gym.Application.Common.Sms;

namespace Gym.Application.Notifications.GetSmsCredit;

/// <summary>
/// The SMS account's remaining credit, for the settings page (BUSINESS_RULES.md §10 <i>Sending</i>).
/// Owner only. Asks the provider each time: it costs nothing, and the page is opened rarely.
/// </summary>
public sealed class GetSmsCreditHandler(ISmsAccount account)
{
    public async Task<SmsCreditResponse> Handle(CancellationToken cancellationToken) =>
        SmsCreditResponse.From(await account.GetCreditAsync(cancellationToken));
}

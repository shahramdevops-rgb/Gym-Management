using Gym.Application.Common.Sms;

namespace Gym.Application.Notifications.GetSmsCredit;

/// <param name="IsTestMode">Nothing is really sent (<c>Sms:Provider</c> is <c>Fake</c>), so there is no credit to show.</param>
/// <param name="RemainingToman">
/// The credit left, in Toman like every amount here (the provider's Rial ÷ 10); <c>null</c> in test
/// mode or when the provider did not answer.
/// </param>
public sealed record SmsCreditResponse(bool IsTestMode, decimal? RemainingToman)
{
    public static SmsCreditResponse From(SmsCredit credit)
    {
        ArgumentNullException.ThrowIfNull(credit);

        return new SmsCreditResponse(credit.IsTestMode, credit.RemainingRial / 10m);
    }
}

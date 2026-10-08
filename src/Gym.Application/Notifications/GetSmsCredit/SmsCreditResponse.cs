using Gym.Application.Common.Sms;

namespace Gym.Application.Notifications.GetSmsCredit;

/// <param name="IsTestMode">Nothing is really sent (<c>Sms:Provider</c> is <c>Fake</c>), so there is no credit to show.</param>
/// <param name="RemainingToman">
/// The credit left, in Toman like every amount here (the provider's Rial ÷ 10); <c>null</c> in test
/// mode or when the provider did not answer.
/// </param>
/// <param name="CreditUsedUp">
/// The warning on both SMS pages (BUSINESS_RULES.md §10 <i>The credit warning</i>, task 10.5): the
/// latest message that failed for credit used up is newer than the latest one sent. Read from the
/// messages, not from the provider, so it shows even when the provider does not answer.
/// </param>
public sealed record SmsCreditResponse(bool IsTestMode, decimal? RemainingToman, bool CreditUsedUp)
{
    public static SmsCreditResponse From(SmsCredit credit, bool creditUsedUp)
    {
        ArgumentNullException.ThrowIfNull(credit);

        return new SmsCreditResponse(credit.IsTestMode, credit.RemainingRial / 10m, creditUsedUp);
    }
}

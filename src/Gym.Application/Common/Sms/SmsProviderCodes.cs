namespace Gym.Application.Common.Sms;

/// <summary>
/// The provider's codes the app reads back from a stored message, long after the send that brought
/// them (BUSINESS_RULES.md §10 <i>Sending</i>). The sender sorts every other code into an outcome and
/// only keeps it for the Owner to read.
/// </summary>
public static class SmsProviderCodes
{
    /// <summary>
    /// Kavenegar's 418: the account's credit is used up. The SMS pages warn while the latest message
    /// that failed with it is newer than the latest one sent (task 10.5).
    /// </summary>
    public const int CreditUsedUp = 418;
}

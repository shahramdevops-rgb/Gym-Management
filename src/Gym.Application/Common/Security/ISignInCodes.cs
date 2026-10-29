namespace Gym.Application.Common.Security;

/// <summary>
/// Computes a phone's one-time code from the seed its <c>SignInCode</c> row holds, with a secret
/// from the server's configuration (<c>SiteAuth:CodeSecret</c>, BUSINESS_RULES.md §15).
/// </summary>
/// <remarks>
/// The database holds the seed and never the code, and the secret is never in the database: a
/// backup alone cannot give anyone a code. The same phone and seed always give the same code,
/// which is how «ارسال دوباره» sends the same code again without it being stored.
/// </remarks>
public interface ISignInCodes
{
    /// <summary>The code, <c>SignInCode.Digits</c> long, leading zeros included.</summary>
    string CodeFor(string phoneNumber, string codeSeed);
}

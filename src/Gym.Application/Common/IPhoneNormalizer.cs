using Gym.Domain.Common;

namespace Gym.Application.Common;

/// <summary>
/// Turns a phone number as someone typed it into E.164 (<c>+989121234567</c>), or says why it
/// cannot (BUSINESS_RULES.md §2: Iranian mobile numbers only).
/// </summary>
/// <remarks>
/// Every phone number goes through this before it is saved or searched for. The unique index on
/// members' phone numbers compares strings; it only prevents duplicates if every spelling of a
/// number (<c>0912…</c>, <c>۰۹۱۲…</c>, <c>+98 912…</c>) arrives as the same string.
/// </remarks>
public interface IPhoneNormalizer
{
    /// <summary>
    /// Fails with <c>MemberErrors.PhoneInvalid</c>, <c>PhoneNotMobile</c> or <c>PhoneNotIranian</c>.
    /// </summary>
    Result<string> Normalize(string input);
}

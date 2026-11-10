using Gym.Application.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members;

/// <summary>
/// The one search box's rules (BUSINESS_RULES.md §2 and §16 <i>Phone search</i>): a name, the last 4
/// digits of a phone, or a whole number. Shared by the member list, the front desk's check-in box
/// (both <see cref="ListMembers.ListMembersHandler"/>) and the «کاربران سایت» page, so a phone is
/// searched the same way everywhere.
/// </summary>
/// <remarks>
/// <para>
/// One box takes either a name or a phone number, so the search decides which from what was typed:
/// only digits, <c>+</c>, spaces, dashes and brackets is a phone search; anything else is a name
/// search, compared with <c>NormalizedFullName</c>, so "علي" typed on an Arabic keyboard finds "علی".
/// </para>
/// <para>
/// A phone search: <b>exactly 4 digits</b> match the end of the stored number, compared as typed, so
/// «0567» finds a number ending in 0567; <b>a whole number</b>, in any format, is normalized exactly
/// as it was when saved and finds that one member; <b>anything else finds nothing</b> — the start of
/// a number, its middle, or any other count of digits (the developer, 1405/08/19).
/// </para>
/// <para>
/// Both are <c>LIKE</c> patterns that start with a wildcard. An ordinary index cannot help those;
/// the trigram (GIN) indexes in <c>MemberConfiguration</c> can.
/// </para>
/// </remarks>
public sealed class MemberSearch(IPhoneNormalizer phones)
{
    /// <summary>How many of the last digits a phone search takes (§16 <i>Phone search</i>).</summary>
    public const int PhoneEndingDigits = 4;

    private const string LikeEscape = "\\";

    /// <param name="text">The search as typed, already through <c>PersianText.Normalize</c> (Persian digits become ASCII).</param>
    public IQueryable<Member> Apply(IQueryable<Member> members, string text)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(text);

        if (!LooksLikePhone(text))
        {
            // The column is stored lower-cased, so lower-casing the input makes the search
            // case-insensitive for Latin names without a function on the column.
            var pattern = $"%{EscapeLike(text.ToLowerInvariant())}%";

            return members.Where(member => EF.Functions.Like(member.NormalizedFullName, pattern, LikeEscape));
        }

        // Looked at before the whole number: four digits are never a valid number, and must not
        // become one by some reading of the normalizer.
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == PhoneEndingDigits)
        {
            // Digits only, so nothing in the pattern needs escaping. E.164 keeps every digit after
            // the country code, so a zero inside the last four is still there to match.
            return members.Where(member => EF.Functions.Like(member.PhoneNumber, $"%{digits}"));
        }

        var phone = phones.Normalize(text);

        return phone.IsSuccess
            ? members.Where(member => member.PhoneNumber == phone.Value)
            : members.Where(_ => false);
    }

    private static bool LooksLikePhone(string text) =>
        text.Any(char.IsAsciiDigit) && text.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')');

    /// <summary>A typed <c>%</c> or <c>_</c> is a character to find, not a wildcard.</summary>
    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}

using System.Text;

namespace Gym.Domain.Common.Text;

/// <summary>
/// Persian text normalization (BUSINESS_RULES.md §13).
/// </summary>
/// <remarks>
/// <para>
/// A Persian keyboard and an Arabic keyboard produce different code points for letters a
/// reader sees as the same: "علي" and "علی" look identical and compare unequal. Search runs on
/// the normalized form, so both find the same member.
/// </para>
/// <para>
/// Every special character is written as a numeric code point. Invisible characters (the
/// zero-width non-joiner, direction marks) and look-alikes cannot be seen in review, and editors
/// and tools have been known to turn a <c>\u</c> escape into the character itself.
/// </para>
/// <para>
/// In Domain because it is a business rule with no dependencies: the Member entity uses it to
/// keep its search column in step with the name it was given.
/// </para>
/// </remarks>
public static class PersianText
{
    private const char PersianZero = (char)0x06F0; // ۰
    private const char ArabicZero = (char)0x0660; // ٠

    private const char ArabicYe = (char)0x064A;
    private const char AlefMaksura = (char)0x0649;
    private const char PersianYe = (char)0x06CC;
    private const char ArabicKaf = (char)0x0643;
    private const char PersianKaf = (char)0x06A9;

    private const char FirstHaraka = (char)0x064B; // fathatan
    private const char LastHaraka = (char)0x0652; // sukun
    private const char Tatweel = (char)0x0640;

    private const char ZeroWidthNonJoiner = (char)0x200C;

    /// <summary>Zero-width space, zero-width joiner, LTR mark, RTL mark, byte order mark.</summary>
    private static readonly char[] InvisibleMarks =
        [(char)0x200B, (char)0x200D, (char)0x200E, (char)0x200F, (char)0xFEFF];

    /// <summary>Persian (۰–۹) and Arabic-Indic (٠–٩) digits to English ones; nothing else changes.</summary>
    public static string NormalizeDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(ToEnglishDigit(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The full §13 pipeline: Persian letters, no vowel marks or tatweel, half-space as space,
    /// no invisible marks, English digits, and single spaces with no leading or trailing ones.
    /// </summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var original in value)
        {
            var character = original switch
            {
                ArabicYe or AlefMaksura => PersianYe,
                ArabicKaf => PersianKaf,

                // Users type the half-space and the space interchangeably.
                ZeroWidthNonJoiner => ' ',
                _ => ToEnglishDigit(original),
            };

            // Visual only: vowel marks, tatweel and invisible marks carry no meaning for search.
            if (character is >= FirstHaraka and <= LastHaraka or Tatweel || InvisibleMarks.Contains(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static char ToEnglishDigit(char character) => character switch
    {
        >= PersianZero and <= (char)(PersianZero + 9) => (char)('0' + (character - PersianZero)),
        >= ArabicZero and <= (char)(ArabicZero + 9) => (char)('0' + (character - ArabicZero)),
        _ => character,
    };
}

using System.Text;

namespace Gym.Application.Common.Text;

/// <summary>
/// Persian (۰–۹) and Arabic-Indic (٠–٩) digits to English ones. CLAUDE.md: every input accepts
/// both; the server stores and compares English digits only.
/// </summary>
/// <remarks>
/// The frontend converts too, but a rule enforced only in one client is not a rule: another
/// client, a script, or a seed password typed with Persian digits would otherwise produce a
/// different password from the same keystrokes.
/// </remarks>
public static class Digits
{
    private const char PersianZero = '۰';
    private const char ArabicZero = '٠';

    public static string ToEnglish(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                >= PersianZero and <= (char)(PersianZero + 9) => (char)('0' + (character - PersianZero)),
                >= ArabicZero and <= (char)(ArabicZero + 9) => (char)('0' + (character - ArabicZero)),
                _ => character,
            });
        }

        return builder.ToString();
    }
}

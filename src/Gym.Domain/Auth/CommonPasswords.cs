using System.Collections.Frozen;
using System.Reflection;

namespace Gym.Domain.Auth;

/// <summary>
/// The blocklist half of <see cref="PasswordPolicy"/>: the 100,000 most common leaked passwords,
/// plus the gym's own name and words anyone would guess for this gym.
/// </summary>
/// <remarks>
/// <para>
/// The list ships inside the assembly (<c>CommonPasswords.txt</c>, an embedded resource), so no
/// password ever leaves the server to be checked, and there is no outside service to be down.
/// </para>
/// <para>
/// A password is common if, lowercased, it is on the list whole, or its core is: the part left
/// after trimming digits, symbols and spaces from both ends. That is what catches the usual
/// disguise, a common word with a year or <c>!</c> added (<c>Football2024!</c>). The comparison is
/// equality, not "contains", so a long passphrase that happens to include a common word passes.
/// </para>
/// <para>
/// The gym's own names are the exception, checked by <see cref="ContainsGymName"/>: they are refused
/// wherever they appear, with a typo or without. Everyone who knows the gym tries its name first, so
/// <c>pasargadplas</c> is as guessable as <c>pasargadplus</c>, which no equality check would catch.
/// </para>
/// </remarks>
public static class CommonPasswords
{
    private const string ResourceName = "Gym.Domain.Auth.CommonPasswords.txt";

    /// <summary>
    /// The gym's name and the Finglish words for what it is. Refused anywhere in a password. Long
    /// and distinctive enough that no ordinary passphrase contains one by accident.
    /// </summary>
    private static readonly string[] GymNames = ["pasargad", "bashgah", "varzesh", "badansazi"];

    /// <summary>
    /// Shorter or ordinary English words about a gym. Common as a whole password, but a passphrase
    /// may contain them ("I train at the gym every morning"), so they are checked by equality like
    /// the leaked list.
    /// </summary>
    private static readonly string[] GymWords = ["gym", "gymplus", "fitness", "bodybuilding"];

    private static readonly Lazy<FrozenSet<string>> Entries = new(Load);

    public static bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var lower = password.ToLowerInvariant();
        var withoutSpaces = string.Concat(lower.Where(character => !char.IsWhiteSpace(character)));

        return IsListed(lower) || IsListed(Core(lower)) || IsListed(Core(withoutSpaces));
    }

    /// <summary>
    /// Whether the password holds one of the gym's own names, ignoring case, spaces, digits and
    /// symbols, and the usual look-alike substitutions (<c>p@sargad</c>, <c>P4SARG4D</c>).
    /// </summary>
    public static bool ContainsGymName(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var letters = string.Concat(password.ToLowerInvariant().Select(UnLeet).Where(IsLetter));

        return GymNames.Any(name => letters.Contains(name, StringComparison.Ordinal));
    }

    /// <summary>The digit or symbol a letter is usually swapped for, turned back into the letter.</summary>
    private static char UnLeet(char character) => character switch
    {
        '@' or '4' => 'a',
        '0' => 'o',
        '1' or '!' => 'i',
        '3' => 'e',
        '$' or '5' => 's',
        _ => character,
    };

    private static bool IsListed(string candidate) => candidate.Length > 0 && Entries.Value.Contains(candidate);

    /// <summary>What is left after trimming everything but English letters from both ends.</summary>
    private static string Core(string lower)
    {
        var start = 0;
        var end = lower.Length;

        while (start < end && !IsLetter(lower[start]))
        {
            start++;
        }

        while (end > start && !IsLetter(lower[end - 1]))
        {
            end--;
        }

        return lower[start..end];
    }

    private static bool IsLetter(char character) => character is >= 'a' and <= 'z';

    private static FrozenSet<string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);

        var entries = new HashSet<string>(GymWords, StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                entries.Add(line);
            }
        }

        return entries.ToFrozenSet(StringComparer.Ordinal);
    }
}

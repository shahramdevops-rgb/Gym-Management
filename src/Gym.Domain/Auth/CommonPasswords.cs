using System.Collections.Frozen;
using System.Reflection;

namespace Gym.Domain.Auth;

/// <summary>
/// The blocklist half of <see cref="PasswordPolicy"/>: the 100,000 most common leaked passwords,
/// plus words anyone would guess for this gym.
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
/// </remarks>
public static class CommonPasswords
{
    private const string ResourceName = "Gym.Domain.Auth.CommonPasswords.txt";

    /// <summary>
    /// The gym's own name and the words for what it is, in English and in Finglish. Not in any
    /// leaked list, and the first thing someone who knows the gym would try.
    /// </summary>
    private static readonly string[] GymWords =
    [
        "pasargad", "pasargadgym", "pasargadgymplus", "gympasargad", "gymplus", "gym",
        "bashgah", "bashgahe", "varzesh", "varzeshi", "badansazi", "fitness", "bodybuilding",
    ];

    private static readonly Lazy<FrozenSet<string>> Entries = new(Load);

    public static bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var lower = password.ToLowerInvariant();
        var withoutSpaces = string.Concat(lower.Where(character => !char.IsWhiteSpace(character)));

        return IsListed(lower) || IsListed(Core(lower)) || IsListed(Core(withoutSpaces));
    }

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

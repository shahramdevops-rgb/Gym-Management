namespace Gym.Domain.Notifications;

/// <summary>
/// The values that fill a template's blanks (BUSINESS_RULES.md §10 <i>Sending</i>). The names are
/// Kavenegar's: <c>%token</c> in a template is filled from <see cref="Token"/>, and so on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why values and not a finished text.</b> The Persian wording is a template approved in the
/// provider's panel; the system only sends what changes from one message to the next. That is what
/// lets the wording change without a release, and what reaches members who blocked advertising SMS.
/// </para>
/// <para>
/// <b>The blanks' limits are checked here, once.</b> <see cref="Token"/>, <see cref="Token2"/> and
/// <see cref="Token3"/> hold no space, <see cref="Token10"/> up to 5 spaces and
/// <see cref="Token20"/> up to 8, each at most 100 characters. A value that breaks them cannot be
/// built, so the provider never refuses a message for it. A caller with free text (a name, a payee)
/// passes it through <see cref="Fit"/> first.
/// </para>
/// </remarks>
/// <param name="Token">
/// Required: Kavenegar refuses a template message without it, so every template uses <c>%token</c>.
/// </param>
public sealed record SmsTokens(
    string Token,
    string? Token2 = null,
    string? Token3 = null,
    string? Token10 = null,
    string? Token20 = null)
{
    public const int MaxLength = 100;

    /// <summary>The spaces <see cref="Token"/>, <see cref="Token2"/> and <see cref="Token3"/> allow.</summary>
    public const int MaxSpacesInShortToken = 0;

    public const int MaxSpacesInToken10 = 5;

    public const int MaxSpacesInToken20 = 8;

    public string Token { get; } = Check(Token, MaxSpacesInShortToken, nameof(Token))
        ?? throw new ArgumentException("Every template message needs a value for %token.", nameof(Token));

    public string? Token2 { get; } = Check(Token2, MaxSpacesInShortToken, nameof(Token2));

    public string? Token3 { get; } = Check(Token3, MaxSpacesInShortToken, nameof(Token3));

    public string? Token10 { get; } = Check(Token10, MaxSpacesInToken10, nameof(Token10));

    public string? Token20 { get; } = Check(Token20, MaxSpacesInToken20, nameof(Token20));

    /// <summary>
    /// Shortens a value to a blank's limits: runs of white space become one space, the words after
    /// <paramref name="maxSpaces"/> spaces are dropped, and what is left is cut at
    /// <see cref="MaxLength"/> characters. A long payee or name is simply cut short (decided with the
    /// developer, 1405/07/14).
    /// </summary>
    public static string Fit(string value, int maxSpaces)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSpaces);

        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kept = string.Join(' ', words.Take(maxSpaces + 1));

        return kept.Length <= MaxLength ? kept : kept[..MaxLength].TrimEnd();
    }

    /// <summary>
    /// An empty or blank value counts as no value: the provider would send the blank empty anyway.
    /// Anything else must already fit, because a value built in code that does not is a bug.
    /// </summary>
    private static string? Check(string? value, int maxSpaces, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"A blank holds at most {MaxLength} characters.", name);
        }

        if (value.Count(char.IsWhiteSpace) > maxSpaces)
        {
            throw new ArgumentException($"This blank holds at most {maxSpaces} spaces.", name);
        }

        return value;
    }
}

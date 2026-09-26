namespace Gym.Application.Common.Security;

/// <summary>
/// BUSINESS_RULES.md §1: 3 to 50 characters from Identity's default set. Shared by the
/// validator and by Identity's options, so the two cannot disagree.
/// </summary>
public static class UserNamePolicy
{
    public const int MinimumLength = 3;

    public const int MaximumLength = 50;

    /// <summary>Identity's default set: Latin letters, digits and <c>- . _ @ +</c>.</summary>
    public const string AllowedCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static bool HasOnlyAllowedCharacters(string? userName) =>
        userName is not null && userName.All(AllowedCharacters.Contains);

    /// <summary>
    /// The names an attacker tries first (BUSINESS_RULES.md §1 *Lockout*): a user name that is
    /// easy to guess is half of a login, and all an attacker needs to lock the account out.
    /// </summary>
    private static readonly string[] GuessableNames =
    [
        "admin", "administrator", "owner", "manager", "modir", "root", "test", "user", "staff",
        "gym", "support", "superuser", "system", "guest", "operator", "reception", "paziresh",
        "karmand", "pasargad", "bashgah",
    ];

    /// <summary>
    /// A guessable name, even with digits or separators added: <c>Owner</c>, <c>admin1</c> and
    /// <c>modir_2</c> are refused, <c>owner.reza</c> is not.
    /// </summary>
    public static bool IsGuessable(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        var letters = string.Concat(userName.Where(char.IsAsciiLetter)).ToLowerInvariant();

        return GuessableNames.Contains(letters, StringComparer.Ordinal);
    }
}

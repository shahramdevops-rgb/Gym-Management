using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// What makes a password acceptable (BUSINESS_RULES.md §1), following NIST SP 800-63B: length
/// and a blocklist, not composition rules.
/// </summary>
/// <remarks>
/// <para>
/// No upper case, digit or symbol is required. People answer such rules with <c>Ali@12345</c>,
/// which satisfies all of them and is among the first guesses an attacker tries. A long password
/// that is not on anyone's list is what actually resists guessing, so that is what is checked.
/// </para>
/// <para>
/// In Domain, like <c>PersianText</c>, because it is a business rule with no dependencies. It is
/// used by the FluentValidation rules (a field error before anything is saved), by Identity's
/// password validator (every password Identity stores, whichever code path set it), and at login
/// (a stored password that no longer passes must be changed).
/// </para>
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    /// <summary>Password hashing is slow on purpose, so length is capped, the same as at login.</summary>
    public const int MaximumLength = 128;

    /// <summary>Fewer different characters than this is a repetition, such as <c>abababababab</c>.</summary>
    public const int MinimumDistinctCharacters = 5;

    private const char FirstAllowed = ' ';
    private const char LastAllowed = '~';

    /// <summary>
    /// Runs of characters people type without thinking. A password that is any stretch of one of
    /// these, forwards or backwards, is refused. The digit run repeats so that it wraps from 9 to 0.
    /// </summary>
    private static readonly string[] Sequences =
    [
        "abcdefghijklmnopqrstuvwxyz",
        "01234567890123456789",
        "qwertyuiopasdfghjklzxcvbnm",
        "1qaz2wsx3edc4rfv5tgb6yhn7ujm8ik9ol0p",
        "!@#$%^&*()_+",
    ];

    /// <summary>
    /// The first rule the password breaks, or success. The caller converts Persian digits to
    /// English ones first (BUSINESS_RULES.md §1), so a digit typed on a Persian keyboard is not
    /// mistaken for a non-English character.
    /// </summary>
    /// <param name="userName">The account's user name; null when it is not known yet.</param>
    public static Result Check(string? password, string? userName)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return Result.Failure(string.IsNullOrEmpty(password) || IsEnglishOnly(password)
                ? PasswordErrors.TooShort
                : PasswordErrors.NotEnglish);
        }

        if (password.Length > MaximumLength)
        {
            return Result.Failure(PasswordErrors.TooLong);
        }

        if (!IsEnglishOnly(password))
        {
            return Result.Failure(PasswordErrors.NotEnglish);
        }

        if (ContainsUserName(password, userName))
        {
            return Result.Failure(PasswordErrors.ContainsUserName);
        }

        if (IsTooSimple(password))
        {
            return Result.Failure(PasswordErrors.TooSimple);
        }

        if (CommonPasswords.Contains(password))
        {
            return Result.Failure(PasswordErrors.TooCommon);
        }

        return Result.Success();
    }

    /// <summary>
    /// Printable ASCII only: English letters, digits, symbols and the space. Persian letters are
    /// refused because the same-looking letter has different code points on different keyboards
    /// (ی/ي, ک/ك), so a password set on one device could fail on another.
    /// </summary>
    public static bool IsEnglishOnly(string? password) =>
        password is not null && password.All(character => character is >= FirstAllowed and <= LastAllowed);

    /// <summary>Ignoring case: <c>Reza.Karimi2024</c> contains <c>reza.karimi</c>.</summary>
    public static bool ContainsUserName(string password, string? userName) =>
        !string.IsNullOrWhiteSpace(userName) &&
        password.Contains(userName.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Too few different characters, or one stretch of a keyboard or alphabet run.</summary>
    public static bool IsTooSimple(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var lower = password.ToLowerInvariant();
        if (lower.Distinct().Count() < MinimumDistinctCharacters)
        {
            return true;
        }

        return Sequences.Any(sequence =>
            sequence.Contains(lower, StringComparison.Ordinal) ||
            new string([.. sequence.Reverse()]).Contains(lower, StringComparison.Ordinal));
    }
}

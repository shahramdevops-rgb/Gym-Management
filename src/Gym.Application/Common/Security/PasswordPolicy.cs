namespace Gym.Application.Common.Security;

/// <summary>
/// BUSINESS_RULES.md §1: at least 8 characters, with a letter and a digit, no case or symbol
/// requirement.
/// </summary>
/// <remarks>
/// Written once and used twice: the FluentValidation rules give the form a field-level error
/// before anything is saved, and Identity's options and password validator enforce the same
/// rule on every password it stores, whichever code path set it.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    /// <summary>The same limit as login: hashing is slow on purpose, so length is capped.</summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// A letter and a digit from any script, so Persian letters and Persian digits count.
    /// </summary>
    public static bool HasLetterAndDigit(string? password) =>
        password is not null && password.Any(char.IsLetter) && password.Any(char.IsDigit);
}

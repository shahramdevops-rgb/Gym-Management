using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// One error per <see cref="PasswordPolicy"/> rule. The codes are the contract with the frontend,
/// which shows each one under the password field in Persian.
/// </summary>
public static class PasswordErrors
{
    public static readonly Error TooShort = Error.Validation(
        "Auth.PasswordTooShort",
        $"Password must be at least {PasswordPolicy.MinimumLength} characters.");

    public static readonly Error TooLong = Error.Validation(
        "Auth.PasswordTooLong",
        "Password is too long.");

    public static readonly Error NotEnglish = Error.Validation(
        "Auth.PasswordNotEnglish",
        "Password may contain only English letters, digits, symbols and spaces.");

    public static readonly Error ContainsUserName = Error.Validation(
        "Auth.PasswordContainsUserName",
        "Password must not contain the user name.");

    public static readonly Error ContainsGymName = Error.Validation(
        "Auth.PasswordContainsGymName",
        "Password must not contain the gym's name.");

    public static readonly Error TooSimple = Error.Validation(
        "Auth.PasswordTooSimple",
        "Password is a repetition or a keyboard sequence.");

    public static readonly Error TooCommon = Error.Validation(
        "Auth.PasswordTooCommon",
        "Password is on the list of common passwords.");

    public static IReadOnlyList<Error> All { get; } = [TooShort, TooLong, NotEnglish, ContainsUserName, ContainsGymName, TooSimple, TooCommon];
}

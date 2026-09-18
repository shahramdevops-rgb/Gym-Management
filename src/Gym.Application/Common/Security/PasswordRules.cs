using FluentValidation;

namespace Gym.Application.Common.Security;

/// <summary>
/// <see cref="PasswordPolicy"/> as one reusable FluentValidation rule, for every command that
/// sets a password: change password, create staff, reset a staff password.
/// </summary>
public static class PasswordRules
{
    /// <summary>
    /// Stops at the first failure, so the form shows the one thing to fix rather than three
    /// messages about an empty field.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidNewPassword<T>(this IRuleBuilderInitial<T, string> rule, string requiredCode) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(requiredCode).WithMessage("Password is required.")
            .MinimumLength(PasswordPolicy.MinimumLength).WithErrorCode("Auth.PasswordTooShort")
                .WithMessage($"Password must be at least {PasswordPolicy.MinimumLength} characters.")
            .MaximumLength(PasswordPolicy.MaximumLength).WithErrorCode("Auth.PasswordTooLong")
                .WithMessage("Password is too long.")
            .Must(PasswordPolicy.HasLetterAndDigit).WithErrorCode("Auth.PasswordRequiresLetterAndDigit")
                .WithMessage("Password must contain at least one letter and one digit.");
}

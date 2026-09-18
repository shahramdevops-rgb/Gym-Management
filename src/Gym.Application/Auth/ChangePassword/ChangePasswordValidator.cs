using FluentValidation;

using Gym.Application.Common.Security;

namespace Gym.Application.Auth.ChangePassword;

/// <summary>
/// The new password's shape, per <see cref="PasswordPolicy"/>, so the form can show which rule
/// failed before anything is checked against the database.
/// </summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithErrorCode("Auth.CurrentPasswordRequired").WithMessage("Current password is required.")
            .MaximumLength(PasswordPolicy.MaximumLength).WithErrorCode("Auth.PasswordTooLong").WithMessage("Password is too long.");

        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Auth.NewPasswordRequired").WithMessage("New password is required.")
            .MinimumLength(PasswordPolicy.MinimumLength).WithErrorCode("Auth.PasswordTooShort")
                .WithMessage($"Password must be at least {PasswordPolicy.MinimumLength} characters.")
            .MaximumLength(PasswordPolicy.MaximumLength).WithErrorCode("Auth.PasswordTooLong").WithMessage("Password is too long.")
            .Must(PasswordPolicy.HasLetterAndDigit).WithErrorCode("Auth.PasswordRequiresLetterAndDigit")
                .WithMessage("Password must contain at least one letter and one digit.");
    }
}

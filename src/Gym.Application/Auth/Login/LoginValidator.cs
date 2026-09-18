using FluentValidation;

namespace Gym.Application.Auth.Login;

/// <summary>
/// Shape checks only. Whether the credentials are right is the handler's job, and it answers
/// with <c>AuthErrors</c>, not with a validation error.
/// </summary>
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Identity's own column length for user names.</summary>
    public const int UserNameMaxLength = 256;

    /// <summary>
    /// Password hashing is deliberately slow, so an unbounded password would let one request
    /// burn CPU. 128 is far longer than any password a person types.
    /// </summary>
    public const int PasswordMaxLength = 128;

    public LoginValidator()
    {
        RuleFor(command => command.UserName)
            .NotEmpty().WithErrorCode("Auth.UserNameRequired").WithMessage("User name is required.")
            .MaximumLength(UserNameMaxLength).WithErrorCode("Auth.UserNameTooLong").WithMessage("User name is too long.");

        RuleFor(command => command.Password)
            .NotEmpty().WithErrorCode("Auth.PasswordRequired").WithMessage("Password is required.")
            .MaximumLength(PasswordMaxLength).WithErrorCode("Auth.PasswordTooLong").WithMessage("Password is too long.");
    }
}

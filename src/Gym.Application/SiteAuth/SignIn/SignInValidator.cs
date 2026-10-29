using FluentValidation;

using Gym.Application.Members;
using Gym.Domain.SiteAuth;

namespace Gym.Application.SiteAuth.SignIn;

/// <summary>Shape only. Whether the code is right is the entity's to say, and it costs a try.</summary>
public sealed class SignInValidator : AbstractValidator<SignInCommand>
{
    /// <summary>Room for spaces around the 5 digits; anything longer is not a code.</summary>
    public const int CodeMaxLength = 10;

    public SignInValidator()
    {
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();

        RuleFor(command => command.Code)
            .NotEmpty().WithErrorCode("SiteAuth.CodeRequired").WithMessage("The code is required.")
            .MaximumLength(CodeMaxLength)
            .WithErrorCode(SignInCodeErrors.Wrong.Code).WithMessage(SignInCodeErrors.Wrong.Description);
    }
}

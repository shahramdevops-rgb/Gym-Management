using FluentValidation;

using Gym.Application.Members;
using Gym.Domain.SiteAuth;

namespace Gym.Application.SiteAuth.SignUp;

/// <summary>The members' own rules for the name and the birth date (BUSINESS_RULES.md §2).</summary>
public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    /// <summary>A signed JWT is a few hundred characters; anything far longer is not one of ours.</summary>
    public const int TicketMaxLength = 2000;

    public SignUpValidator()
    {
        RuleFor(command => command.Ticket)
            .NotEmpty()
            .MaximumLength(TicketMaxLength)
            .WithErrorCode(SiteAuthErrors.SignUpExpired.Code).WithMessage(SiteAuthErrors.SignUpExpired.Description);

        RuleFor(command => command.FullName).ValidFullName();
        RuleFor(command => command.BirthDate).RequiredBirthDate();
    }
}

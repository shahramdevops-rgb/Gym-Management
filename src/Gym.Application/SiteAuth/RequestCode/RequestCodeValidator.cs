using FluentValidation;

using Gym.Application.Members;

namespace Gym.Application.SiteAuth.RequestCode;

/// <summary>Shape only, with the members' own phone rule; the handler normalizes the number.</summary>
public sealed class RequestCodeValidator : AbstractValidator<RequestCodeCommand>
{
    public RequestCodeValidator()
    {
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();
    }
}

using FluentValidation;

namespace Gym.Application.Members.CreateMember;

public sealed class CreateMemberValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberValidator()
    {
        RuleFor(command => command.FullName).ValidFullName();
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();
        RuleFor(command => command.Notes).ValidNotes();
    }
}

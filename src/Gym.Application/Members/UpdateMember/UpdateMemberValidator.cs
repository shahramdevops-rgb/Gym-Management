using FluentValidation;

namespace Gym.Application.Members.UpdateMember;

public sealed class UpdateMemberValidator : AbstractValidator<UpdateMemberCommand>
{
    public UpdateMemberValidator()
    {
        RuleFor(command => command.FullName).ValidFullName();
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();
        RuleFor(command => command.Notes).ValidNotes();
    }
}

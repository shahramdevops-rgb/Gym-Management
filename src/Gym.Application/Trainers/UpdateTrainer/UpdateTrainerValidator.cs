using FluentValidation;

using Gym.Application.Members;

namespace Gym.Application.Trainers.UpdateTrainer;

public sealed class UpdateTrainerValidator : AbstractValidator<UpdateTrainerCommand>
{
    public UpdateTrainerValidator()
    {
        RuleFor(command => command.FullName).ValidTrainerName();
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();
    }
}

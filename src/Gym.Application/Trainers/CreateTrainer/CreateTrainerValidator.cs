using FluentValidation;

using Gym.Application.Members;

namespace Gym.Application.Trainers.CreateTrainer;

public sealed class CreateTrainerValidator : AbstractValidator<CreateTrainerCommand>
{
    public CreateTrainerValidator()
    {
        RuleFor(command => command.FullName).ValidTrainerName();
        RuleFor(command => command.PhoneNumber).ValidPhoneInput();
    }
}

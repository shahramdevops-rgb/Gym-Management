using FluentValidation;

namespace Gym.Application.Lockers.CreateLocker;

public sealed class CreateLockerValidator : AbstractValidator<CreateLockerCommand>
{
    public CreateLockerValidator()
    {
        RuleFor(command => command.Number).ValidNumber();
    }
}

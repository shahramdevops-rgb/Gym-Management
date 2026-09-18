using FluentValidation;

namespace Gym.Application.Plans.CreatePlan;

public sealed class CreatePlanValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanValidator()
    {
        RuleFor(command => command.Name).ValidName();
        RuleFor(command => command.DurationDays).ValidDurationDays();
        RuleFor(command => command.SessionCount).ValidSessionCount();
        RuleFor(command => command.Price).ValidPrice();
    }
}

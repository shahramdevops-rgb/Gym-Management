using FluentValidation;

namespace Gym.Application.Plans.UpdatePlan;

public sealed class UpdatePlanValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanValidator()
    {
        RuleFor(command => command.Name).ValidName();
        RuleFor(command => command.DurationDays).ValidDurationDays();
        RuleFor(command => command.SessionCount).ValidSessionCount();
        RuleFor(command => command.Price).ValidPrice();
    }
}

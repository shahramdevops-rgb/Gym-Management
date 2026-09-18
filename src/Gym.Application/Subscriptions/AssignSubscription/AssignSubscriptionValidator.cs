using FluentValidation;

using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions.AssignSubscription;

public sealed class AssignSubscriptionValidator : AbstractValidator<AssignSubscriptionCommand>
{
    public AssignSubscriptionValidator()
    {
        RuleFor(command => command.PlanId)
            .NotEmpty()
            .WithErrorCode(SubscriptionErrors.PlanRequired.Code)
            .WithMessage(SubscriptionErrors.PlanRequired.Description);
    }
}

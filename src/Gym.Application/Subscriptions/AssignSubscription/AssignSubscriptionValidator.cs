using FluentValidation;

using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>
/// The same limits <see cref="Subscription.CreateMembership"/> enforces, checked first so the form
/// hears about the field before anything touches the database.
/// </summary>
public sealed class AssignSubscriptionValidator : AbstractValidator<AssignSubscriptionCommand>
{
    public AssignSubscriptionValidator()
    {
        RuleFor(command => command.SessionCount)
            .GreaterThanOrEqualTo(Subscription.MinSessionCount)
            .WithErrorCode(SubscriptionErrors.SessionCountTooLow.Code)
            .WithMessage(SubscriptionErrors.SessionCountTooLow.Description);

        RuleFor(command => command.SessionCount)
            .LessThanOrEqualTo(Subscription.MaxSessionCount)
            .WithErrorCode(SubscriptionErrors.SessionCountTooHigh.Code)
            .WithMessage(SubscriptionErrors.SessionCountTooHigh.Description);
    }
}

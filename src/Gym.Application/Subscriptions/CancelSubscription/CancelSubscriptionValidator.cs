using FluentValidation;

using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithErrorCode(SubscriptionErrors.CancelReasonRequired.Code)
            .WithMessage(SubscriptionErrors.CancelReasonRequired.Description)
            .MaximumLength(Subscription.CancellationReasonMaxLength)
            .WithErrorCode(SubscriptionErrors.CancelReasonTooLong.Code)
            .WithMessage(SubscriptionErrors.CancelReasonTooLong.Description);
    }
}

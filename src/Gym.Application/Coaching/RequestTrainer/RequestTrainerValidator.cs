using FluentValidation;

using Gym.Domain.Coaching;

namespace Gym.Application.Coaching.RequestTrainer;

/// <summary>Shape only; whether the trainer exists and is a trainer now is the handler's.</summary>
public sealed class RequestTrainerValidator : AbstractValidator<RequestTrainerCommand>
{
    public RequestTrainerValidator()
    {
        RuleFor(command => command.TrainerId)
            .NotEmpty()
            .WithErrorCode(CoachingErrors.TrainerNotFound.Code).WithMessage(CoachingErrors.TrainerNotFound.Description);
    }
}

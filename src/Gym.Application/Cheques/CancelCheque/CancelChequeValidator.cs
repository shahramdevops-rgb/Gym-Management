using FluentValidation;

namespace Gym.Application.Cheques.CancelCheque;

public sealed class CancelChequeValidator : AbstractValidator<CancelChequeCommand>
{
    public CancelChequeValidator()
    {
        RuleFor(command => command.Reason).ValidCancelReason();
    }
}

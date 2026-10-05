using FluentValidation;

using Gym.Domain.Payables;

namespace Gym.Application.Payables.CancelPayable;

public sealed class CancelPayableValidator : AbstractValidator<CancelPayableCommand>
{
    public CancelPayableValidator()
    {
        RuleFor(command => command.Reason)
            .ValidReason(PayableErrors.CancelReasonRequired, PayableErrors.CancelReasonTooLong);
    }
}

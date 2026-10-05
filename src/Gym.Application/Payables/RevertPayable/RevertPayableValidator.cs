using FluentValidation;

using Gym.Domain.Payables;

namespace Gym.Application.Payables.RevertPayable;

public sealed class RevertPayableValidator : AbstractValidator<RevertPayableCommand>
{
    public RevertPayableValidator()
    {
        RuleFor(command => command.Reason)
            .ValidReason(PayableErrors.RevertReasonRequired, PayableErrors.RevertReasonTooLong);
    }
}

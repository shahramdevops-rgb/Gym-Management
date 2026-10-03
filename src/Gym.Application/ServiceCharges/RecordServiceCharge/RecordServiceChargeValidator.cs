using FluentValidation;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

public sealed class RecordServiceChargeValidator : AbstractValidator<RecordServiceChargeCommand>
{
    public RecordServiceChargeValidator()
    {
        // A miscellaneous sale has its own endpoint, which takes its name and quantity.
        RuleFor(command => command.Kind)
            .ValidKind()
            .NotEqual(ServiceChargeKind.Miscellaneous)
            .WithErrorCode(ServiceChargeErrors.KindInvalid.Code).WithMessage(ServiceChargeErrors.KindInvalid.Description);
        RuleFor(command => command.Amount).ValidAmount();
    }
}

using FluentValidation;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

public sealed class RecordServiceChargeValidator : AbstractValidator<RecordServiceChargeCommand>
{
    public RecordServiceChargeValidator()
    {
        RuleFor(command => command.Kind).ValidKind();
        RuleFor(command => command.Amount).ValidAmount();
    }
}

using FluentValidation;

namespace Gym.Application.ServiceCharges.VoidServiceCharge;

public sealed class VoidServiceChargeValidator : AbstractValidator<VoidServiceChargeCommand>
{
    public VoidServiceChargeValidator()
    {
        RuleFor(command => command.Reason).ValidVoidReason();
    }
}

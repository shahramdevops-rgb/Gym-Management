using FluentValidation;

namespace Gym.Application.ServiceCharges.ChangeServiceChargeAmount;

public sealed class ChangeServiceChargeAmountValidator : AbstractValidator<ChangeServiceChargeAmountCommand>
{
    public ChangeServiceChargeAmountValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
    }
}

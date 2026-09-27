using FluentValidation;

namespace Gym.Application.Pricing.UpdatePrices;

public sealed class UpdatePricesValidator : AbstractValidator<UpdatePricesCommand>
{
    public UpdatePricesValidator()
    {
        RuleFor(command => command.SessionPrice).ValidPrice();
        RuleFor(command => command.SingleVisitPrice).ValidPrice();
    }
}

using FluentValidation;

using Gym.Domain.Pricing;

namespace Gym.Application.Pricing;

/// <summary>
/// The money rule from <see cref="PriceList.CheckPrice"/>, one check per error, so each failure
/// carries its own code for the form and the validator cannot disagree with the entity.
/// </summary>
public static class PricingRules
{
    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(price => PriceList.CheckPrice(price) != PricingErrors.PriceNegative)
            .WithErrorCode(PricingErrors.PriceNegative.Code).WithMessage(PricingErrors.PriceNegative.Description)
            .Must(price => PriceList.CheckPrice(price) != PricingErrors.PriceTooLarge)
            .WithErrorCode(PricingErrors.PriceTooLarge.Code).WithMessage(PricingErrors.PriceTooLarge.Description)
            .Must(price => PriceList.CheckPrice(price) != PricingErrors.PriceTooManyDecimals)
            .WithErrorCode(PricingErrors.PriceTooManyDecimals.Code)
            .WithMessage(PricingErrors.PriceTooManyDecimals.Description);
}

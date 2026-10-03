using FluentValidation;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordShopSale;

public sealed class RecordShopSaleValidator : AbstractValidator<RecordShopSaleCommand>
{
    public RecordShopSaleValidator()
    {
        RuleFor(command => command.Items)
            .Must(items => items is { Count: > 0 })
            .WithErrorCode(ServiceChargeErrors.ShopItemsRequired.Code).WithMessage(ServiceChargeErrors.ShopItemsRequired.Description)
            .Must(items => items is null || items.Count <= ServiceCharge.MaxShopItemsPerSale)
            .WithErrorCode(ServiceChargeErrors.TooManyShopItems.Code).WithMessage(ServiceChargeErrors.TooManyShopItems.Description);

        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.Description)
                .Cascade(CascadeMode.Stop)
                .Must(description => !string.IsNullOrWhiteSpace(description))
                .WithErrorCode(ServiceChargeErrors.DescriptionRequired.Code).WithMessage(ServiceChargeErrors.DescriptionRequired.Description)
                .Must(description => description.Trim().Length <= ServiceCharge.DescriptionMaxLength)
                .WithErrorCode(ServiceChargeErrors.DescriptionTooLong.Code).WithMessage(ServiceChargeErrors.DescriptionTooLong.Description);

            item.RuleFor(line => line.Quantity)
                .InclusiveBetween(1, ServiceCharge.MaxQuantity)
                .WithErrorCode(ServiceChargeErrors.QuantityInvalid.Code).WithMessage(ServiceChargeErrors.QuantityInvalid.Description);

            item.RuleFor(line => line.UnitPrice).ValidAmount();
        });
    }
}

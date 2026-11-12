using FluentValidation;

namespace Gym.Application.Shop.UpdateShopProduct;

public sealed class UpdateShopProductValidator : AbstractValidator<UpdateShopProductCommand>
{
    public UpdateShopProductValidator()
    {
        RuleFor(command => command.Name).ValidShopProductName();
        RuleFor(command => command.Description).ValidShopDescription();
        RuleFor(command => command.CategoryId).ValidShopCategoryId();
        RuleFor(command => command.Variants).ValidShopVariantCount();
        RuleForEach(command => command.Variants).SetValidator(new ShopCatalogRules.VariantValidator());
    }
}

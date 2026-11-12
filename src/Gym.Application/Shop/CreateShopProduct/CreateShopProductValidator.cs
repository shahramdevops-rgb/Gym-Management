using FluentValidation;

namespace Gym.Application.Shop.CreateShopProduct;

public sealed class CreateShopProductValidator : AbstractValidator<CreateShopProductCommand>
{
    public CreateShopProductValidator()
    {
        RuleFor(command => command.Name).ValidShopProductName();
        RuleFor(command => command.Description).ValidShopDescription();
        RuleFor(command => command.CategoryId).ValidShopCategoryId();
        RuleFor(command => command.Variants).ValidShopVariantCount();
        RuleForEach(command => command.Variants).SetValidator(new ShopCatalogRules.VariantValidator());
    }
}

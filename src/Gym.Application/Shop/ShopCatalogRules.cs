using FluentValidation;

using Gym.Domain.Shop;

namespace Gym.Application.Shop;

/// <summary>
/// The field checks the shop's forms share, with the entities' error codes, so the form gets a
/// message under each field before anything touches the database. The limits come from the
/// entities, so the validator and the entity cannot disagree; the rules across variants (labels
/// unique, empty only when alone) are the entity's.
/// </summary>
public static class ShopCatalogRules
{
    public static IRuleBuilderOptions<T, string> ValidShopCategoryName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(ShopCategoryErrors.NameRequired.Code).WithMessage(ShopCategoryErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= ShopCategory.NameMaxLength)
            .WithErrorCode(ShopCategoryErrors.NameTooLong.Code).WithMessage(ShopCategoryErrors.NameTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidShopProductName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(ShopProductErrors.NameRequired.Code).WithMessage(ShopProductErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= ShopProduct.NameMaxLength)
            .WithErrorCode(ShopProductErrors.NameTooLong.Code).WithMessage(ShopProductErrors.NameTooLong.Description);

    public static IRuleBuilderOptions<T, string?> ValidShopDescription<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(description => description is null || description.Trim().Length <= ShopProduct.DescriptionMaxLength)
            .WithErrorCode(ShopProductErrors.DescriptionTooLong.Code)
            .WithMessage(ShopProductErrors.DescriptionTooLong.Description);

    public static IRuleBuilderOptions<T, Guid> ValidShopCategoryId<T>(this IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty()
            .WithErrorCode(ShopProductErrors.CategoryRequired.Code).WithMessage(ShopProductErrors.CategoryRequired.Description);

    public static IRuleBuilderOptions<T, IReadOnlyList<ShopVariantInput>> ValidShopVariantCount<T>(
        this IRuleBuilderInitial<T, IReadOnlyList<ShopVariantInput>> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(variants => variants is { Count: > 0 })
            .WithErrorCode(ShopProductErrors.VariantsRequired.Code).WithMessage(ShopProductErrors.VariantsRequired.Description)
            .Must(variants => variants.Count <= ShopProduct.MaxVariants)
            .WithErrorCode(ShopProductErrors.TooManyVariants.Code).WithMessage(ShopProductErrors.TooManyVariants.Description);

    /// <summary>One variant's own fields, each under its own name (<c>variants[1].price</c>).</summary>
    public sealed class VariantValidator : AbstractValidator<ShopVariantInput>
    {
        public VariantValidator()
        {
            RuleFor(variant => variant.Label)
                .Must(label => label is not null && label.Trim().Length <= ShopVariant.LabelMaxLength)
                .WithErrorCode(ShopProductErrors.VariantLabelTooLong.Code)
                .WithMessage(ShopProductErrors.VariantLabelTooLong.Description);

            RuleFor(variant => variant.Price).Cascade(CascadeMode.Stop)
                .Must(price => ShopVariant.CheckPrice(price) != ShopProductErrors.VariantPriceNotPositive)
                .WithErrorCode(ShopProductErrors.VariantPriceNotPositive.Code)
                .WithMessage(ShopProductErrors.VariantPriceNotPositive.Description)
                .Must(price => ShopVariant.CheckPrice(price) != ShopProductErrors.VariantPriceTooLarge)
                .WithErrorCode(ShopProductErrors.VariantPriceTooLarge.Code)
                .WithMessage(ShopProductErrors.VariantPriceTooLarge.Description)
                .Must(price => ShopVariant.CheckPrice(price) != ShopProductErrors.VariantPriceTooManyDecimals)
                .WithErrorCode(ShopProductErrors.VariantPriceTooManyDecimals.Code)
                .WithMessage(ShopProductErrors.VariantPriceTooManyDecimals.Description);

            RuleFor(variant => variant.Stock)
                .InclusiveBetween(0, ShopVariant.MaxStock)
                .WithErrorCode(ShopProductErrors.VariantStockOutOfRange.Code)
                .WithMessage(ShopProductErrors.VariantStockOutOfRange.Description);
        }
    }
}

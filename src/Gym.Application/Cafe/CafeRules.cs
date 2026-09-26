using FluentValidation;

using Gym.Domain.Cafe;

namespace Gym.Application.Cafe;

/// <summary>
/// The field checks create and update share, with the entities' error codes, so the form gets a
/// message per field before anything touches the database. The limits come from
/// <see cref="Product"/> and <see cref="ProductCategory"/>, so the validator and the entity
/// cannot disagree.
/// </summary>
public static class CafeRules
{
    public static IRuleBuilderOptions<T, string> ValidProductName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(ProductErrors.NameRequired.Code).WithMessage(ProductErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= Product.NameMaxLength)
            .WithErrorCode(ProductErrors.NameTooLong.Code).WithMessage(ProductErrors.NameTooLong.Description);

    public static IRuleBuilderOptions<T, Guid> ValidCategoryId<T>(this IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty()
            .WithErrorCode(ProductErrors.CategoryRequired.Code).WithMessage(ProductErrors.CategoryRequired.Description);

    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidProductPrice<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(price => Product.CheckPrice(price) != ProductErrors.PriceNegative)
            .WithErrorCode(ProductErrors.PriceNegative.Code).WithMessage(ProductErrors.PriceNegative.Description)
            .Must(price => Product.CheckPrice(price) != ProductErrors.PriceTooLarge)
            .WithErrorCode(ProductErrors.PriceTooLarge.Code).WithMessage(ProductErrors.PriceTooLarge.Description)
            .Must(price => Product.CheckPrice(price) != ProductErrors.PriceTooManyDecimals)
            .WithErrorCode(ProductErrors.PriceTooManyDecimals.Code)
            .WithMessage(ProductErrors.PriceTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, string> ValidCategoryName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(ProductCategoryErrors.NameRequired.Code)
            .WithMessage(ProductCategoryErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= ProductCategory.NameMaxLength)
            .WithErrorCode(ProductCategoryErrors.NameTooLong.Code)
            .WithMessage(ProductCategoryErrors.NameTooLong.Description);
}

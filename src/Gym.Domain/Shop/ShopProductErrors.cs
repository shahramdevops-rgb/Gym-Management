using Gym.Domain.Common;

namespace Gym.Domain.Shop;

public static class ShopProductErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ShopProducts.NotFound",
        "No shop product has that id.");

    public static readonly Error NameRequired = Error.Validation(
        "ShopProducts.NameRequired",
        "A product name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "ShopProducts.NameTooLong",
        $"A product name is at most {ShopProduct.NameMaxLength} characters.");

    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ShopProducts.NameAlreadyExists",
        "Another shop product already uses that name.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "ShopProducts.DescriptionTooLong",
        $"A description is at most {ShopProduct.DescriptionMaxLength} characters.");

    public static readonly Error CategoryRequired = Error.Validation(
        "ShopProducts.CategoryRequired",
        "A product must be in a category.");

    /// <summary>The category id is well formed but names no category.</summary>
    public static readonly Error CategoryNotFound = Error.NotFound(
        "ShopProducts.CategoryNotFound",
        "No shop category has that id.");

    public static readonly Error VariantsRequired = Error.Validation(
        "ShopProducts.VariantsRequired",
        "A product has at least one variant.");

    public static readonly Error TooManyVariants = Error.Validation(
        "ShopProducts.TooManyVariants",
        $"A product has at most {ShopProduct.MaxVariants} variants.");

    /// <summary>A label may be empty only when the product has a single variant (§16 <i>Products</i>).</summary>
    public static readonly Error VariantLabelRequired = Error.Validation(
        "ShopProducts.VariantLabelRequired",
        "Each variant needs a label when a product has more than one.");

    public static readonly Error VariantLabelTooLong = Error.Validation(
        "ShopProducts.VariantLabelTooLong",
        $"A variant label is at most {ShopVariant.LabelMaxLength} characters.");

    public static readonly Error VariantLabelDuplicate = Error.Validation(
        "ShopProducts.VariantLabelDuplicate",
        "Two variants of one product have the same label.");

    public static readonly Error VariantPriceNotPositive = Error.Validation(
        "ShopProducts.VariantPriceNotPositive",
        "A variant's price must be above zero.");

    public static readonly Error VariantPriceTooLarge = Error.Validation(
        "ShopProducts.VariantPriceTooLarge",
        "A variant's price is too large.");

    public static readonly Error VariantPriceTooManyDecimals = Error.Validation(
        "ShopProducts.VariantPriceTooManyDecimals",
        $"A price has at most {ShopVariant.PriceDecimals} decimal places.");

    public static readonly Error VariantStockOutOfRange = Error.Validation(
        "ShopProducts.VariantStockOutOfRange",
        $"Stock is a whole number from 0 to {ShopVariant.MaxStock}.");

    /// <summary>The form sent a variant id this product does not have.</summary>
    public static readonly Error VariantNotFound = Error.NotFound(
        "ShopProducts.VariantNotFound",
        "The product has no variant with that id.");

    public static readonly Error TooManyPhotos = Error.BusinessRule(
        "ShopProducts.TooManyPhotos",
        $"A product has at most {ShopProduct.MaxPhotos} photos.");

    public static readonly Error PhotoNotFound = Error.NotFound(
        "ShopProducts.PhotoNotFound",
        "The product has no photo with that id.");

    /// <summary>A new order must name every photo of the product once, and nothing else.</summary>
    public static readonly Error PhotoOrderInvalid = Error.Validation(
        "ShopProducts.PhotoOrderInvalid",
        "The new order must list each of the product's photos exactly once.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ShopProducts.ChangedConcurrently",
        "The product was changed by someone else since it was read. Reload and try again.");
}

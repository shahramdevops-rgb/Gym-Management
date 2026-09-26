using Gym.Domain.Common;

namespace Gym.Domain.Cafe;

public static class ProductErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Products.NotFound",
        "No product has that id.");

    /// <summary>
    /// BUSINESS_RULES.md §8: unique across the whole cafe, inactive products included, in
    /// normalized form — not merely within one category.
    /// </summary>
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "Products.NameAlreadyExists",
        "Another product already uses that name.");

    public static readonly Error NameRequired = Error.Validation(
        "Products.NameRequired",
        "Product name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "Products.NameTooLong",
        "Product name is too long.");

    public static readonly Error CategoryRequired = Error.Validation(
        "Products.CategoryRequired",
        "A product must belong to a category.");

    /// <summary>The category id is well formed but names no category.</summary>
    public static readonly Error CategoryNotFound = Error.NotFound(
        "Products.CategoryNotFound",
        "No product category has that id.");

    public static readonly Error PriceNegative = Error.Validation(
        "Products.PriceNegative",
        "Price cannot be negative.");

    public static readonly Error PriceTooLarge = Error.Validation(
        "Products.PriceTooLarge",
        "Price is too large.");

    public static readonly Error PriceTooManyDecimals = Error.Validation(
        "Products.PriceTooManyDecimals",
        $"Price can have at most {Product.PriceDecimals} decimal places.");

    /// <summary>BUSINESS_RULES.md §8: an inactive product is not sold.</summary>
    public static readonly Error Inactive = Error.BusinessRule(
        "Products.Inactive",
        "The product is inactive and cannot be sold.");

    /// <summary>Two people edited the same product at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Products.ChangedConcurrently",
        "The product was changed by someone else at the same moment. Reload and try again.");
}

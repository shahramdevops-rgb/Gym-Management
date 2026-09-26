using Gym.Domain.Common;

namespace Gym.Domain.Cafe;

public static class ProductCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ProductCategories.NotFound",
        "No product category has that id.");

    /// <summary>
    /// Names are unique among all categories, compared in normalized form, for the same reason
    /// plan names are: two headings that look alike at the till are a coin flip.
    /// </summary>
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ProductCategories.NameAlreadyExists",
        "Another product category already uses that name.");

    public static readonly Error NameRequired = Error.Validation(
        "ProductCategories.NameRequired",
        "Category name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "ProductCategories.NameTooLong",
        "Category name is too long.");

    /// <summary>
    /// BUSINESS_RULES.md §8: a category is deleted only while it is empty. Deleting one with
    /// products would either orphan them or delete part of the price list by surprise.
    /// </summary>
    public static readonly Error NotEmpty = Error.Conflict(
        "ProductCategories.NotEmpty",
        "The category still has products and cannot be deleted.");

    /// <summary>Two people edited the same category at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ProductCategories.ChangedConcurrently",
        "The category was changed by someone else at the same moment. Reload and try again.");
}

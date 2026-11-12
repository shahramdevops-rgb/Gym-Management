using Gym.Domain.Common;

namespace Gym.Domain.Shop;

public static class ShopCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ShopCategories.NotFound",
        "No shop category has that id.");

    public static readonly Error NameRequired = Error.Validation(
        "ShopCategories.NameRequired",
        "A category name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "ShopCategories.NameTooLong",
        $"A category name is at most {ShopCategory.NameMaxLength} characters.");

    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ShopCategories.NameAlreadyExists",
        "Another shop category already uses that name.");

    /// <summary>BUSINESS_RULES.md §16 <i>Products</i>: a category is deleted only while it is empty.</summary>
    public static readonly Error NotEmpty = Error.BusinessRule(
        "ShopCategories.NotEmpty",
        "The category still has products and cannot be deleted.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ShopCategories.ChangedConcurrently",
        "The category was changed by someone else since it was read. Reload and try again.");
}

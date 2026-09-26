namespace Gym.Application.Cafe;

/// <summary>
/// Database index names the cafe handlers react to, shared with the configurations that create
/// them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class CafeConstraints
{
    public const string UniqueProductName = "ix_products_normalized_name";

    public const string UniqueCategoryName = "ix_product_categories_normalized_name";

    /// <summary>
    /// The foreign key from a product to its category. Deleting a category that still has
    /// products violates it, which is the database's half of "deleted only while empty".
    /// </summary>
    public const string ProductCategoryForeignKey = "fk_products_product_categories_category_id";
}

namespace Gym.Application.Shop;

/// <summary>
/// Database constraint names the shop's handlers react to, shared with the configurations and the
/// migration that create them (see <c>CafeConstraints</c>).
/// </summary>
public static class ShopCatalogConstraints
{
    public const string UniqueCategoryName = "ux_shop_categories_normalized_name";

    public const string UniqueProductName = "ux_shop_products_normalized_name";

    /// <summary>
    /// A product's category. Restrict: deleting a category that still has products violates it, the
    /// database's half of "a category is deleted only while it is empty".
    /// </summary>
    public const string ProductCategoryForeignKey = "fk_shop_products_shop_categories_category_id";

    /// <summary>
    /// Deferred to the commit (written by hand in migration AddShopCatalog), so a reorder may pass
    /// through two photos on one position. Two uploads at the same moment both taking the next
    /// position end here.
    /// </summary>
    public const string UniquePhotoPosition = "ux_shop_photos_product_id_position";

    /// <summary>Deferred like <see cref="UniquePhotoPosition"/>.</summary>
    public const string UniqueVariantPosition = "ux_shop_variants_product_id_position";

    /// <summary>Deferred, so two variants may swap labels in one save.</summary>
    public const string UniqueVariantLabel = "ux_shop_variants_product_id_normalized_label";
}

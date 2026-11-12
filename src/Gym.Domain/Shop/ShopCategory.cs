using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Shop;

/// <summary>
/// A heading of the online shop: «مکمل», «پوشاک» (BUSINESS_RULES.md §16 <i>Products</i>). One level,
/// no sub-categories (the developer, 1405/08/21); a product sits in exactly one.
/// </summary>
/// <remarks>
/// The online shop's own table, apart from the cafe's <c>product_categories</c>: the shop is "an extra
/// online sale, and nothing more" (§16), sharing no stock or list with the counter. A category carries
/// no money, so it may be deleted, but only while no product is in it; that is a question about the
/// products' table, so the handler asks it and the foreign key is the safety net.
/// </remarks>
public sealed class ShopCategory : Entity
{
    public const int NameMaxLength = 40;

    // For EF Core.
    private ShopCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> through <see cref="PersianText.Normalize"/>, in lower case; the unique index
    /// is on this column, so a name typed with an Arabic ye is still a duplicate.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Postgres <c>xmin</c>: a stale rename cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public static Result<ShopCategory> Create(string name)
    {
        var category = new ShopCategory();
        var renamed = category.Rename(name);

        return renamed.IsSuccess ? category : Result.Failure<ShopCategory>(renamed.Error);
    }

    public Result Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(ShopCategoryErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(ShopCategoryErrors.NameTooLong);
        }

        Name = cleanName;
        NormalizedName = PersianText.Normalize(cleanName).ToLowerInvariant();

        return Result.Success();
    }
}

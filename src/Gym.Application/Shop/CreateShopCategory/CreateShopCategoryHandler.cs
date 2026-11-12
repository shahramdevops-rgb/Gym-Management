using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.CreateShopCategory;

/// <summary>Adds a category to the online shop (BUSINESS_RULES.md §16 <i>Products</i>). Shop admin only.</summary>
public sealed class CreateShopCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ShopCategoryResponse>> Handle(CreateShopCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = ShopCategory.Create(command.Name);
        if (created.IsFailure)
        {
            return Result.Failure<ShopCategoryResponse>(created.Error);
        }

        var category = created.Value;
        if (await db.ShopCategories.AnyAsync(c => c.NormalizedName == category.NormalizedName, cancellationToken))
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.NameAlreadyExists);
        }

        // Two requests can pass the check above at once; the unique index decides.
        db.ShopCategories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniqueCategoryName)
        {
            return Result.Failure<ShopCategoryResponse>(ShopCategoryErrors.NameAlreadyExists);
        }

        return ShopCategoryResponse.From(category, productCount: 0);
    }
}

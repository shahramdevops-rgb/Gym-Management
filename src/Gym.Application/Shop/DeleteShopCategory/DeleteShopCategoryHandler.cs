using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.DeleteShopCategory;

/// <summary>
/// Deletes a shop category while no product is in it (BUSINESS_RULES.md §16 <i>Products</i>). It
/// carries no money and no history, so it is really deleted. Shop admin only.
/// </summary>
public sealed class DeleteShopCategoryHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.ShopCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ShopCategoryErrors.NotFound);
        }

        if (await db.ShopProducts.AnyAsync(p => p.CategoryId == id, cancellationToken))
        {
            return Result.Failure(ShopCategoryErrors.NotEmpty);
        }

        // A product added between the check and the delete: the restricting foreign key decides.
        db.ShopCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ForeignKeyConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.ProductCategoryForeignKey)
        {
            return Result.Failure(ShopCategoryErrors.NotEmpty);
        }

        return Result.Success();
    }
}

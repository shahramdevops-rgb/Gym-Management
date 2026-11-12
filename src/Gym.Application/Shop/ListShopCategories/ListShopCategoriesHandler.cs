using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.ListShopCategories;

/// <summary>The shop's categories by name, each with its number of products. Shop admin or Owner.</summary>
public sealed class ListShopCategoriesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ShopCategoryResponse>> Handle(CancellationToken cancellationToken) =>
        await db.ShopCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new ShopCategoryResponse(
                category.Id,
                category.Name,
                db.ShopProducts.Count(product => product.CategoryId == category.Id),
                category.Version))
            .ToListAsync(cancellationToken);
}

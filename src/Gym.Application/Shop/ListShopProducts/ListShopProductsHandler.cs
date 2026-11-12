using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.ListShopProducts;

/// <summary>
/// Every product of the shop, hidden ones too, newest first, as the shop admin's list shows them.
/// Not paged: a gym's shop holds tens of products, and the list filters by category on the page.
/// Shop admin or Owner.
/// </summary>
public sealed class ListShopProductsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ShopProductListItem>> Handle(CancellationToken cancellationToken) =>
        await db.ShopProducts
            .AsNoTracking()
            .OrderByDescending(product => product.CreatedAt)
            .Select(product => new ShopProductListItem(
                product.Id,
                product.Name,
                product.CategoryId,
                db.ShopCategories.Where(c => c.Id == product.CategoryId).Select(c => c.Name).Single(),
                product.IsVisible,
                product.Photos.Where(photo => photo.Position == 0).Select(photo => (Guid?)photo.Id).FirstOrDefault(),
                product.Variants.Min(variant => variant.Price),
                product.Variants.Max(variant => variant.Price),
                product.Variants.Sum(variant => variant.Stock),
                product.Variants.Count))
            .ToListAsync(cancellationToken);
}

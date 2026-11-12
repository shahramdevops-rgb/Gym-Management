using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.GetShopProduct;

/// <summary>One product with its variants and photos, for its form. Shop admin or Owner.</summary>
public sealed class GetShopProductHandler(IAppDbContext db)
{
    public async Task<Result<ShopProductResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.ShopProducts
            .AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null
            ? Result.Failure<ShopProductResponse>(ShopProductErrors.NotFound)
            : ShopProductResponse.From(product);
    }
}

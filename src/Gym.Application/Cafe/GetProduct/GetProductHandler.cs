using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.GetProduct;

/// <summary>One product, active or not, with the name of its category.</summary>
public sealed class GetProductHandler(IAppDbContext db)
{
    public async Task<Result<ProductResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Join(
                db.ProductCategories.AsNoTracking(),
                p => p.CategoryId,
                category => category.Id,
                ProductWithCategory.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? Result.Failure<ProductResponse>(ProductErrors.NotFound) : product;
    }
}

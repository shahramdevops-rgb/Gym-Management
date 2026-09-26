using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.SetProductCategoryActive;

/// <summary>
/// Switches a whole shelf on or off (BUSINESS_RULES.md §8). Switching a category off takes every
/// product under it out of the till in one action — which is the point of having the switch here
/// rather than only on each product.
/// </summary>
/// <remarks>
/// Nothing is written to the products themselves: "sellable" is read as the product's switch and
/// its category's switch together, so switching the shelf back on restores exactly the products
/// that were switched on before. Copying the flag down to each product would have to be undone
/// correctly, and it could not be.
/// </remarks>
public sealed class SetProductCategoryActiveHandler(IAppDbContext db)
{
    public Task<Result<ProductCategoryResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        Change(id, category => category.Activate(), cancellationToken);

    public Task<Result<ProductCategoryResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Change(id, category => category.Deactivate(), cancellationToken);

    private async Task<Result<ProductCategoryResponse>> Change(
        Guid id, Action<ProductCategory> change, CancellationToken cancellationToken)
    {
        var category = await db.ProductCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NotFound);
        }

        change(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.ChangedConcurrently);
        }

        return ProductCategoryResponse.From(category);
    }
}

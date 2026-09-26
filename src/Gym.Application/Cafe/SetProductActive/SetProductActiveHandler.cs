using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.SetProductActive;

/// <summary>
/// Activates or deactivates a product. A deactivated product is no longer sold; orders already
/// rung up are not affected (BUSINESS_RULES.md §8). This is how a product leaves the price list —
/// it is never deleted.
/// </summary>
/// <remarks>
/// Repeating either action succeeds and changes nothing. As with plans, no <c>Version</c> is asked
/// of the client: the request carries only an intent, and <c>xmin</c> still refuses a save that
/// races another one.
/// </remarks>
public sealed class SetProductActiveHandler(IAppDbContext db)
{
    public Task<Result<ProductResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        Change(id, product => product.Activate(), cancellationToken);

    public Task<Result<ProductResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Change(id, product => product.Deactivate(), cancellationToken);

    private async Task<Result<ProductResponse>> Change(
        Guid id, Action<Product> change, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductResponse>(ProductErrors.NotFound);
        }

        var category = await db.ProductCategories
            .SingleOrDefaultAsync(c => c.Id == product.CategoryId, cancellationToken);
        if (category is null)
        {
            // The foreign key makes this impossible; answering instead of dereferencing null
            // keeps the impossible case a 404 rather than a 500.
            return Result.Failure<ProductResponse>(ProductErrors.CategoryNotFound);
        }

        change(product);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ProductResponse>(ProductErrors.ChangedConcurrently);
        }

        return ProductResponse.From(product, category);
    }
}

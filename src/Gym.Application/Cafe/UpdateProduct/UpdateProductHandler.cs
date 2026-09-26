using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.UpdateProduct;

/// <summary>
/// Replaces a product's name, category and price, active or not. Orders already rung up keep
/// their snapshot, so nothing else changes (BUSINESS_RULES.md §8).
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdatePlanHandler</c>: the client's <c>Version</c> refuses an
/// edit made on stale data, and <c>xmin</c> refuses a save that races another.
/// </remarks>
public sealed class UpdateProductHandler(IAppDbContext db)
{
    public async Task<Result<ProductResponse>> Handle(
        Guid id, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductResponse>(ProductErrors.NotFound);
        }

        if (product.Version != command.Version)
        {
            return Result.Failure<ProductResponse>(ProductErrors.ChangedConcurrently);
        }

        var updated = product.Update(command.Name, command.CategoryId, command.Price);
        if (updated.IsFailure)
        {
            return Result.Failure<ProductResponse>(updated.Error);
        }

        var category = await db.ProductCategories
            .SingleOrDefaultAsync(c => c.Id == product.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ProductResponse>(ProductErrors.CategoryNotFound);
        }

        // Keeping its own name is fine; taking another product's is not.
        var normalizedName = product.NormalizedName;
        if (await db.Products.AnyAsync(p => p.NormalizedName == normalizedName && p.Id != id, cancellationToken))
        {
            return Result.Failure<ProductResponse>(ProductErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == CafeConstraints.UniqueProductName)
        {
            return Result.Failure<ProductResponse>(ProductErrors.NameAlreadyExists);
        }
        catch (ForeignKeyConstraintException exception)
            when (exception.ConstraintName == CafeConstraints.ProductCategoryForeignKey)
        {
            return Result.Failure<ProductResponse>(ProductErrors.CategoryNotFound);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ProductResponse>(ProductErrors.ChangedConcurrently);
        }

        return ProductResponse.From(product, category);
    }
}

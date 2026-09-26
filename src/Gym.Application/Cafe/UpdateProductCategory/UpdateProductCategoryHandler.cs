using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.UpdateProductCategory;

/// <summary>
/// Renames a category. Nothing copies a category's name, so this reaches no order and no product
/// (BUSINESS_RULES.md §8).
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdatePlanHandler</c>: the client's <c>Version</c> refuses an
/// edit made on stale data, and <c>xmin</c> refuses a save that races another.
/// </remarks>
public sealed class UpdateProductCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ProductCategoryResponse>> Handle(
        Guid id, UpdateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await db.ProductCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NotFound);
        }

        if (category.Version != command.Version)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.ChangedConcurrently);
        }

        var renamed = category.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return Result.Failure<ProductCategoryResponse>(renamed.Error);
        }

        // Keeping its own name is fine; taking another category's is not.
        var normalizedName = category.NormalizedName;
        if (await db.ProductCategories.AnyAsync(
                c => c.NormalizedName == normalizedName && c.Id != id, cancellationToken))
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == CafeConstraints.UniqueCategoryName)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NameAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.ChangedConcurrently);
        }

        return ProductCategoryResponse.From(category);
    }
}

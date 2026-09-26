using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.DeleteProductCategory;

/// <summary>
/// Removes an empty category (BUSINESS_RULES.md §8). Owner only.
/// </summary>
/// <remarks>
/// The one thing in the cafe that is deleted rather than deactivated: a category is a heading
/// with no money and no history behind it. A category that still has products is refused, because
/// deleting it would either orphan them or take part of the price list away by surprise — and
/// products themselves are never deleted, since orders point at them.
/// </remarks>
public sealed class DeleteProductCategoryHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.ProductCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ProductCategoryErrors.NotFound);
        }

        if (await db.Products.AnyAsync(product => product.CategoryId == id, cancellationToken))
        {
            return Result.Failure(ProductCategoryErrors.NotEmpty);
        }

        // A product can be added between the check above and this save; the foreign key decides.
        db.ProductCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ForeignKeyConstraintException exception)
            when (exception.ConstraintName == CafeConstraints.ProductCategoryForeignKey)
        {
            return Result.Failure(ProductCategoryErrors.NotEmpty);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else deleted it first. Nothing is left to delete, which is what was asked.
            return Result.Failure(ProductCategoryErrors.NotFound);
        }

        return Result.Success();
    }
}

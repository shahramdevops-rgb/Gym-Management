using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.CreateProductCategory;

/// <summary>Adds a heading to the cafe's price list (BUSINESS_RULES.md §8). Owner only.</summary>
public sealed class CreateProductCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ProductCategoryResponse>> Handle(
        CreateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The entity normalizes the name, so build the category first and compare its normalized form.
        var created = ProductCategory.Create(command.Name);
        if (created.IsFailure)
        {
            return Result.Failure<ProductCategoryResponse>(created.Error);
        }

        var category = created.Value;
        if (await db.ProductCategories.AnyAsync(c => c.NormalizedName == category.NormalizedName, cancellationToken))
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NameAlreadyExists);
        }

        // Two requests can pass the check above at once; the unique index decides.
        db.ProductCategories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == CafeConstraints.UniqueCategoryName)
        {
            return Result.Failure<ProductCategoryResponse>(ProductCategoryErrors.NameAlreadyExists);
        }

        return ProductCategoryResponse.From(category);
    }
}

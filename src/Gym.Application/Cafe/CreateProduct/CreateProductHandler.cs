using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.CreateProduct;

/// <summary>
/// Adds a line to the cafe's price list (BUSINESS_RULES.md §8). Front-desk work, both roles: the
/// person who sees a new box arrive is the one who can enter it.
/// </summary>
public sealed class CreateProductHandler(IAppDbContext db)
{
    public async Task<Result<ProductResponse>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The entity normalizes the name, so build the product first and compare its normalized form.
        var created = Product.Create(command.Name, command.CategoryId, command.Price);
        if (created.IsFailure)
        {
            return Result.Failure<ProductResponse>(created.Error);
        }

        var product = created.Value;

        var category = await db.ProductCategories
            .SingleOrDefaultAsync(c => c.Id == product.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ProductResponse>(ProductErrors.CategoryNotFound);
        }

        if (await db.Products.AnyAsync(p => p.NormalizedName == product.NormalizedName, cancellationToken))
        {
            return Result.Failure<ProductResponse>(ProductErrors.NameAlreadyExists);
        }

        // Two requests can pass the checks above at once; the indexes decide.
        db.Products.Add(product);
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
            // The category was deleted between the read above and this save.
            return Result.Failure<ProductResponse>(ProductErrors.CategoryNotFound);
        }

        return ProductResponse.From(product, category);
    }
}

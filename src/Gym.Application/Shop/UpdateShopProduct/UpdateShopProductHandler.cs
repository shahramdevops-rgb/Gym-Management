using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.UpdateShopProduct;

/// <summary>
/// Saves a product's form: name, description, category, shown or hidden, and its variants with their
/// prices and stock (BUSINESS_RULES.md §16 <i>Products</i>). Shop admin only.
/// </summary>
/// <remarks>
/// Two concurrency layers, as the cafe's products: the form's <c>version</c> refuses an edit made on
/// stale data, and <c>xmin</c> refuses a save that races another. The variants' positions and
/// labels are unique per product in the database too, checked at the commit.
/// </remarks>
public sealed class UpdateShopProductHandler(IAppDbContext db)
{
    public async Task<Result<ShopProductResponse>> Handle(
        Guid id, UpdateShopProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await db.ShopProducts
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NotFound);
        }

        if (product.Version != command.Version)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }

        var updated = product.Update(command.Name, command.Description, command.CategoryId, command.IsVisible, command.Variants);
        if (updated.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(updated.Error);
        }

        if (!await db.ShopCategories.AnyAsync(c => c.Id == product.CategoryId, cancellationToken))
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.CategoryNotFound);
        }

        var normalizedName = product.NormalizedName;
        if (await db.ShopProducts.AnyAsync(p => p.NormalizedName == normalizedName && p.Id != id, cancellationToken))
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniqueProductName)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NameAlreadyExists);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName is ShopCatalogConstraints.UniqueVariantPosition or ShopCatalogConstraints.UniqueVariantLabel)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }
        catch (ForeignKeyConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.ProductCategoryForeignKey)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.CategoryNotFound);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }

        return ShopProductResponse.From(product);
    }
}

using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.CreateShopProduct;

/// <summary>
/// Adds a product with its variants (BUSINESS_RULES.md §16 <i>Products</i>); photos are added to it
/// afterwards, one upload each. Shop admin only.
/// </summary>
public sealed class CreateShopProductHandler(IAppDbContext db)
{
    public async Task<Result<ShopProductResponse>> Handle(CreateShopProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // A new product has no variants yet, so any id the form sends names nothing.
        var variants = command.Variants.Select(v => v with { Id = null }).ToList();
        var created = ShopProduct.Create(command.Name, command.Description, command.CategoryId, command.IsVisible, variants);
        if (created.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(created.Error);
        }

        var product = created.Value;
        if (!await db.ShopCategories.AnyAsync(c => c.Id == product.CategoryId, cancellationToken))
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.CategoryNotFound);
        }

        if (await db.ShopProducts.AnyAsync(p => p.NormalizedName == product.NormalizedName, cancellationToken))
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NameAlreadyExists);
        }

        db.ShopProducts.Add(product);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniqueProductName)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NameAlreadyExists);
        }
        catch (ForeignKeyConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.ProductCategoryForeignKey)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.CategoryNotFound);
        }

        return ShopProductResponse.From(product);
    }
}

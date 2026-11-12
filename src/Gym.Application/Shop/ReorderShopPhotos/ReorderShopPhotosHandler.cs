using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.ReorderShopPhotos;

/// <summary>Puts a product's photos in a new order; the first is the main photo. Shop admin only.</summary>
public sealed class ReorderShopPhotosHandler(IAppDbContext db)
{
    public async Task<Result<ShopProductResponse>> Handle(
        Guid productId, ReorderShopPhotosCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await db.ShopProducts
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NotFound);
        }

        var reordered = product.ReorderPhotos(command.PhotoIds ?? []);
        if (reordered.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(reordered.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniquePhotoPosition)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }

        return ShopProductResponse.From(product);
    }
}

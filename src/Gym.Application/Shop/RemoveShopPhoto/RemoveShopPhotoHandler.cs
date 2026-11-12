using Gym.Application.Common;
using Gym.Application.Common.Files;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.RemoveShopPhoto;

/// <summary>
/// Removes a product's photo and closes the gap; the next one becomes the main photo if it was the
/// first. A photo is no financial record, so it is really removed. Shop admin only.
/// </summary>
/// <remarks>The row goes first, then the files (<see cref="IFileStore"/>).</remarks>
public sealed class RemoveShopPhotoHandler(IAppDbContext db, IFileStore files)
{
    public async Task<Result<ShopProductResponse>> Handle(Guid productId, Guid photoId, CancellationToken cancellationToken)
    {
        var product = await db.ShopProducts
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NotFound);
        }

        var removed = product.RemovePhoto(photoId);
        if (removed.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(removed.Error);
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
            // Removed by someone else at the same moment.
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }

        await files.DeleteAsync(removed.Value.LargeKey, CancellationToken.None);
        await files.DeleteAsync(removed.Value.SmallKey, CancellationToken.None);

        return ShopProductResponse.From(product);
    }
}

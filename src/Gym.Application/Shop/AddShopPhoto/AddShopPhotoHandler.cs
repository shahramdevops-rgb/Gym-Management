using Gym.Application.Common;
using Gym.Application.Common.Files;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.AddShopPhoto;

/// <summary>
/// Adds one photo at the end of a product's photos (BUSINESS_RULES.md §16 <i>Products</i>): checked,
/// re-encoded to WebP without its EXIF data, both files written, then the row saved. Shop admin only.
/// </summary>
/// <remarks>
/// The files are written before the row and removed again if the row cannot be saved, so a photo
/// row never points at a missing file (<see cref="IFileStore"/>). Two uploads to one product at the
/// same moment may both take the next position; the deferred unique index refuses the second.
/// </remarks>
public sealed class AddShopPhotoHandler(IAppDbContext db, IImageProcessor images, IFileStore files)
{
    public async Task<Result<ShopProductResponse>> Handle(
        Guid productId, Stream? content, long length, CancellationToken cancellationToken)
    {
        if (content is null || length <= 0)
        {
            return Result.Failure<ShopProductResponse>(PhotoErrors.Required);
        }

        if (length > IImageProcessor.MaxUploadBytes)
        {
            return Result.Failure<ShopProductResponse>(PhotoErrors.TooLarge);
        }

        var product = await db.ShopProducts
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.NotFound);
        }

        // Before decoding: a full product need not spend the time on the photo.
        if (product.Photos.Count >= ShopProduct.MaxPhotos)
        {
            return Result.Failure<ShopProductResponse>(ShopProductErrors.TooManyPhotos);
        }

        var processed = await images.ProcessAsync(content, cancellationToken);
        if (processed.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(processed.Error);
        }

        var image = processed.Value;
        var added = product.AddPhoto(image.Width, image.Height);
        if (added.IsFailure)
        {
            return Result.Failure<ShopProductResponse>(added.Error);
        }

        var photo = added.Value;
        await files.SaveAsync(photo.LargeKey, image.Large, cancellationToken);
        await files.SaveAsync(photo.SmallKey, image.Small, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ShopCatalogConstraints.UniquePhotoPosition)
        {
            await DeleteFilesAsync(photo);
            return Result.Failure<ShopProductResponse>(ShopProductErrors.ChangedConcurrently);
        }
        catch
        {
            await DeleteFilesAsync(photo);
            throw;
        }

        return ShopProductResponse.From(product);
    }

    // Not the request's token: the files must go even when the request was cancelled.
    private async Task DeleteFilesAsync(ShopPhoto photo)
    {
        await files.DeleteAsync(photo.LargeKey, CancellationToken.None);
        await files.DeleteAsync(photo.SmallKey, CancellationToken.None);
    }
}

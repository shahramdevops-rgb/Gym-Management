using Gym.Application.Common;
using Gym.Application.Common.Files;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.GetShopPhoto;

/// <summary>
/// A product photo's file, <c>large</c> or <c>small</c>. Public (ADR 0011: anyone may view a product
/// photo, the site's catalogue from 16.4 included). Only a photo that still has its row is served, so
/// a removed photo is gone even if its file outlived it.
/// </summary>
public sealed class GetShopPhotoHandler(IAppDbContext db, IFileStore files)
{
    public const string Large = "large";

    public const string Small = "small";

    public async Task<Result<Stream>> Handle(Guid photoId, string size, CancellationToken cancellationToken)
    {
        if (size is not (Large or Small) ||
            !await db.ShopPhotos.AnyAsync(photo => photo.Id == photoId, cancellationToken))
        {
            return Result.Failure<Stream>(ShopProductErrors.PhotoNotFound);
        }

        var stream = files.OpenRead(ShopPhoto.KeyOf(photoId, size));
        return stream is null ? Result.Failure<Stream>(ShopProductErrors.PhotoNotFound) : stream;
    }
}

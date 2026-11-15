using Gym.Application.Common;
using Gym.Application.Common.Files;
using Gym.Application.Shop.GetShopPhoto;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.GetShowcasePhoto;

/// <summary>
/// A product photo's file on the gym's site, <c>large</c> or <c>small</c> (BUSINESS_RULES.md §16
/// <i>The showcase</i>). Anyone, without signing in, but only for a shown product: a hidden product
/// is nowhere on the site, its photos included. The panel's own address
/// (<see cref="GetShopPhotoHandler"/>) still serves every photo, so the shop admin sees a hidden
/// product's.
/// </summary>
/// <remarks>
/// A browser that kept a photo before its product was hidden may go on showing its copy (it is
/// cached for a year); a new visitor, or a new address, gets nothing.
/// </remarks>
public sealed class GetShowcasePhotoHandler(IAppDbContext db, IFileStore files)
{
    public async Task<Result<Stream>> Handle(Guid photoId, string size, CancellationToken cancellationToken)
    {
        var shown = size is GetShopPhotoHandler.Large or GetShopPhotoHandler.Small
            && await db.ShopPhotos.AnyAsync(
                photo => photo.Id == photoId
                    && db.ShopProducts.Any(product => product.Id == photo.ProductId && product.IsVisible),
                cancellationToken);

        if (!shown)
        {
            return Result.Failure<Stream>(ShopProductErrors.PhotoNotFound);
        }

        var stream = files.OpenRead(ShopPhoto.KeyOf(photoId, size));
        return stream is null ? Result.Failure<Stream>(ShopProductErrors.PhotoNotFound) : stream;
    }
}

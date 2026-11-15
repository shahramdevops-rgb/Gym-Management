using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.GetShowcaseProduct;

/// <summary>
/// One shown product's page on the gym's site (BUSINESS_RULES.md §16 <i>The showcase</i>): its
/// description, variants (each in stock or not) and photos, with the shop's contact ways. Anyone,
/// without signing in.
/// </summary>
public sealed class GetShowcaseProductHandler(IAppDbContext db)
{
    public async Task<Result<ShowcaseProductResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        // A hidden product answers exactly as one that does not exist: a stranger who kept its
        // address learns nothing from the difference.
        var product = await db.ShopProducts
            .AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == id && p.IsVisible, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ShowcaseProductResponse>(ShopProductErrors.NotFound);
        }

        var categoryName = await db.ShopCategories
            .Where(category => category.Id == product.CategoryId)
            .Select(category => category.Name)
            .SingleAsync(cancellationToken);

        var settings = await ShopSettingsRow.CurrentAsync(db, cancellationToken);

        return new ShowcaseProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.CategoryId,
            categoryName,
            [.. product.Variants.Select(v => new ShowcaseVariant(v.Id, v.Label, v.Price, v.Stock > 0))],
            [.. product.Photos.Select(p => new ShowcasePhoto(p.Id, p.Width, p.Height))],
            ShowcaseContacts.From(settings));
    }
}

using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.GetShowcase;

/// <summary>
/// The shop's showcase on the gym's site (BUSINESS_RULES.md §16 <i>The showcase</i>): every shown
/// product, newest first, the categories that hold one, and the contact ways. Anyone, without
/// signing in.
/// </summary>
/// <remarks>
/// Not paged, as the shop admin's list is not: a gym's shop holds tens of products, and the page
/// filters by category itself. A hidden product is left out here, in the query, so nothing about it
/// ever leaves the server.
/// </remarks>
public sealed class GetShowcaseHandler(IAppDbContext db)
{
    public async Task<ShowcaseResponse> Handle(CancellationToken cancellationToken)
    {
        var products = await db.ShopProducts
            .AsNoTracking()
            .Where(product => product.IsVisible)
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Id)
            .Select(product => new ShowcaseProductItem(
                product.Id,
                product.Name,
                product.CategoryId,
                product.Photos.Where(photo => photo.Position == 0).Select(photo => (Guid?)photo.Id).FirstOrDefault(),
                product.Variants.Min(variant => variant.Price),
                product.Variants.Max(variant => variant.Price),
                product.Variants.Any(variant => variant.Stock > 0)))
            .ToListAsync(cancellationToken);

        // A category whose products are all hidden (or that has none) would be a chip leading to
        // an empty page.
        var categories = await db.ShopCategories
            .AsNoTracking()
            .Where(category => db.ShopProducts.Any(product => product.CategoryId == category.Id && product.IsVisible))
            .OrderBy(category => category.Name)
            .Select(category => new ShowcaseCategory(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        var settings = await ShopSettingsRow.CurrentAsync(db, cancellationToken);

        return new ShowcaseResponse(categories, products, ShowcaseContacts.From(settings));
    }
}

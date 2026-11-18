using Gym.Domain.Shop;

namespace Gym.Application.Shop;

/// <summary>
/// The shop as anyone sees it on the gym's site, without signing in (BUSINESS_RULES.md §16 <i>The
/// showcase</i>, task 16.4): the categories that hold something to show, the shown products newest
/// first, and the shop's contact ways.
/// </summary>
/// <remarks>
/// A shape of its own rather than the shop admin's: nothing here says how many are in stock, whether
/// a product is hidden, or carries a version to edit with. What a stranger may read is decided by
/// what these records hold.
/// </remarks>
public sealed record ShowcaseResponse(
    IReadOnlyList<ShowcaseCategory> Categories,
    IReadOnlyList<ShowcaseProductItem> Products,
    ShowcaseContacts Contacts);

public sealed record ShowcaseCategory(Guid Id, string Name);

/// <summary>
/// One product's card. The main photo is read at <c>/api/site/shop/photos/{id}/small</c>; the two
/// prices differ when its variants do; <paramref name="InStock"/> is "any variant has stock", never
/// the number.
/// </summary>
public sealed record ShowcaseProductItem(
    Guid Id,
    string Name,
    Guid CategoryId,
    Guid? MainPhotoId,
    decimal MinPrice,
    decimal MaxPrice,
    bool InStock);

/// <summary>A variant as a customer sees it: in stock or not, never how many.</summary>
public sealed record ShowcaseVariant(Guid Id, string Label, decimal Price, bool InStock);

/// <summary>A shown product's own page. The contact ways come with it, so the page is one request.</summary>
public sealed record ShowcaseProductResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    IReadOnlyList<ShowcaseVariant> Variants,
    IReadOnlyList<ShowcasePhoto> Photos,
    ShowcaseContacts Contacts);

/// <summary>
/// A photo's id and size, so the page lays it out before it loads; its files are at
/// <c>/api/site/shop/photos/{id}/large</c> and <c>/small</c>. Its own record, not the shop admin's,
/// so a field added there never reaches a stranger.
/// </summary>
public sealed record ShowcasePhoto(Guid Id, int Width, int Height);

/// <summary>The shop's contact ways for customers; null is "not given", and is not shown.</summary>
/// <param name="MobilePhone">E.164, as every mobile is kept.</param>
/// <param name="WhatsAppPhone">E.164, as every mobile is kept.</param>
public sealed record ShowcaseContacts(
    string? MobilePhone,
    string? LandlinePhone,
    string? WhatsAppPhone,
    string? Telegram,
    string? Rubika,
    string? Eitaa,
    string? Bale,
    string? Instagram)
{
    public static ShowcaseContacts From(ShopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ShowcaseContacts(
            settings.MobilePhone,
            settings.LandlinePhone,
            settings.WhatsAppPhone,
            settings.Telegram,
            settings.Rubika,
            settings.Eitaa,
            settings.Bale,
            settings.Instagram);
    }
}

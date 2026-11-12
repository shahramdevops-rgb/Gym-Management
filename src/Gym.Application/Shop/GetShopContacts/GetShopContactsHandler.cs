using Gym.Application.Common;

namespace Gym.Application.Shop.GetShopContacts;

/// <summary>
/// The shop's contact ways as they are saved (BUSINESS_RULES.md §16 <i>The shop admin</i>). The shop
/// admin edits them; the Owner reads them (the developer, 1405/08/20).
/// </summary>
public sealed class GetShopContactsHandler(IAppDbContext db)
{
    public async Task<ShopContactsResponse> Handle(CancellationToken cancellationToken) =>
        ShopContactsResponse.From(await ShopSettingsRow.CurrentAsync(db, cancellationToken));
}

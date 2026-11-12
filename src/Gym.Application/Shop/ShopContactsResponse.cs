using Gym.Domain.Shop;

namespace Gym.Application.Shop;

/// <summary>
/// The ways a customer can reach the shop, as the shop admin's «راه‌های تماس» page shows them
/// (BUSINESS_RULES.md §16 <i>The shop admin</i>). Null is "not given".
/// </summary>
/// <param name="MobilePhone">E.164, as every mobile is kept.</param>
/// <param name="WhatsAppPhone">E.164, as every mobile is kept.</param>
/// <param name="Version">Sent back with an edit, so an edit made on stale values is refused.</param>
public sealed record ShopContactsResponse(
    string? MobilePhone,
    string? LandlinePhone,
    string? WhatsAppPhone,
    string? Telegram,
    string? Rubika,
    string? Eitaa,
    string? Bale,
    string? Instagram,
    uint Version)
{
    public static ShopContactsResponse From(ShopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ShopContactsResponse(
            settings.MobilePhone,
            settings.LandlinePhone,
            settings.WhatsAppPhone,
            settings.Telegram,
            settings.Rubika,
            settings.Eitaa,
            settings.Bale,
            settings.Instagram,
            settings.Version);
    }
}

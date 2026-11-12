namespace Gym.Domain.Shop;

/// <summary>
/// The ways a customer can reach the shop, as <see cref="ShopSettings.SetContacts"/> takes them. Null
/// is "not given".
/// </summary>
public sealed record ShopContacts(
    string? MobilePhone,
    string? LandlinePhone,
    string? WhatsAppPhone,
    string? Telegram,
    string? Rubika,
    string? Eitaa,
    string? Bale,
    string? Instagram);

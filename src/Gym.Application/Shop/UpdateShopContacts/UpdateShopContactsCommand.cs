namespace Gym.Application.Shop.UpdateShopContacts;

/// <summary>Every way at once, as the form saves them. Empty or missing clears a way.</summary>
/// <param name="MobilePhone">As typed: any digits, <c>09…</c> or <c>+98…</c>.</param>
/// <param name="LandlinePhone">As typed, with its area code: <c>07132345678</c>, any digits.</param>
/// <param name="WhatsAppPhone">As typed, like <paramref name="MobilePhone"/>.</param>
/// <param name="Telegram">The id, with or without the <c>@</c>.</param>
/// <param name="Version">The <c>version</c> from the values as they were read before editing.</param>
public sealed record UpdateShopContactsCommand(
    string? MobilePhone,
    string? LandlinePhone,
    string? WhatsAppPhone,
    string? Telegram,
    string? Rubika,
    string? Eitaa,
    string? Bale,
    string? Instagram,
    uint Version);

using Gym.Domain.Common;

namespace Gym.Domain.Shop;

/// <summary>
/// The online shop's own settings (BUSINESS_RULES.md §16 <i>The shop admin</i>). Today only the ways a
/// customer can reach the shop: one set for the whole shop, not one per shop admin (the developer,
/// 1405/08/20), so they stay when a shop admin changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row.</b> Seeded by the migration with <see cref="TheId"/> and every way empty; a check
/// constraint on the id keeps a second one out, like the SMS settings.
/// </para>
/// <para>
/// <b>Kept as they will be used.</b> The phones as E.164 (the mobile and WhatsApp, normalized by the
/// handler as every mobile) or as ten digits after the 0 (the landline); the messengers' ids without
/// the <c>@</c>. Each is optional: an empty one is not shown to customers.
/// </para>
/// </remarks>
public sealed class ShopSettings : Entity
{
    /// <summary>The id of the one row, seeded by the migration.</summary>
    public static readonly Guid TheId = new("5d0c7a13-8e2f-4b6a-9c41-7f3e2a9b6d18");

    // For EF Core. The row comes from the migration, so no code creates one.
    private ShopSettings()
    {
    }

    /// <summary>E.164, <c>+989</c> and nine digits.</summary>
    public string? MobilePhone { get; private set; }

    /// <summary>With its area code, <c>0</c> and ten digits (<c>07132345678</c>).</summary>
    public string? LandlinePhone { get; private set; }

    /// <summary>The WhatsApp number, E.164 like <see cref="MobilePhone"/>.</summary>
    public string? WhatsAppPhone { get; private set; }

    public string? Telegram { get; private set; }

    public string? Rubika { get; private set; }

    public string? Eitaa { get; private set; }

    public string? Bale { get; private set; }

    public string? Instagram { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two saves at the same moment cannot both win.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Replaces every way at once, as the form saves them. The handler has already cleaned the
    /// values; anything still out of shape is refused here, and by the database after.
    /// </summary>
    public Result SetContacts(ShopContacts contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);

        if (!ShopContactRules.IsMobile(contacts.MobilePhone) ||
            !ShopContactRules.IsMobile(contacts.WhatsAppPhone) ||
            !ShopContactRules.IsLandline(contacts.LandlinePhone) ||
            !ShopContactRules.IsHandle(contacts.Telegram) ||
            !ShopContactRules.IsHandle(contacts.Rubika) ||
            !ShopContactRules.IsHandle(contacts.Eitaa) ||
            !ShopContactRules.IsHandle(contacts.Bale) ||
            !ShopContactRules.IsHandle(contacts.Instagram))
        {
            return Result.Failure(ShopErrors.ContactInvalid);
        }

        MobilePhone = contacts.MobilePhone;
        LandlinePhone = contacts.LandlinePhone;
        WhatsAppPhone = contacts.WhatsAppPhone;
        Telegram = contacts.Telegram;
        Rubika = contacts.Rubika;
        Eitaa = contacts.Eitaa;
        Bale = contacts.Bale;
        Instagram = contacts.Instagram;

        return Result.Success();
    }
}

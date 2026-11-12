using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop.UpdateShopContacts;

/// <summary>
/// The shop admin saves the shop's contact ways (BUSINESS_RULES.md §16 <i>The shop admin</i>). The
/// audit log keeps what they were before.
/// </summary>
/// <remarks>
/// Same two concurrency layers as every edit: the client's <c>Version</c> refuses an edit made on
/// stale values (two shop admins at once), and <c>xmin</c> refuses a save that races another one.
/// </remarks>
public sealed class UpdateShopContactsHandler(IAppDbContext db, IPhoneNormalizer phones)
{
    public async Task<Result<ShopContactsResponse>> Handle(UpdateShopContactsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The members' phone rules (§2): any digits, Iranian mobile only, kept as E.164. Each under
        // its own code, so the page shows it under its own field.
        string? mobile = null;
        if (!string.IsNullOrWhiteSpace(command.MobilePhone))
        {
            var phone = phones.Normalize(command.MobilePhone);
            if (phone.IsFailure)
            {
                return Result.Failure<ShopContactsResponse>(ShopErrors.MobilePhoneInvalid);
            }

            mobile = phone.Value;
        }

        string? whatsApp = null;
        if (!string.IsNullOrWhiteSpace(command.WhatsAppPhone))
        {
            var phone = phones.Normalize(command.WhatsAppPhone);
            if (phone.IsFailure)
            {
                return Result.Failure<ShopContactsResponse>(ShopErrors.WhatsAppPhoneInvalid);
            }

            whatsApp = phone.Value;
        }

        var settings = await ShopSettingsRow.ForUpdateAsync(db, cancellationToken);

        if (settings.Version != command.Version)
        {
            return Result.Failure<ShopContactsResponse>(ShopErrors.ChangedConcurrently);
        }

        var updated = settings.SetContacts(new ShopContacts(
            mobile,
            ShopContactText.Landline(command.LandlinePhone),
            whatsApp,
            ShopContactText.Handle(command.Telegram),
            ShopContactText.Handle(command.Rubika),
            ShopContactText.Handle(command.Eitaa),
            ShopContactText.Handle(command.Bale),
            ShopContactText.Handle(command.Instagram)));
        if (updated.IsFailure)
        {
            return Result.Failure<ShopContactsResponse>(updated.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShopContactsResponse>(ShopErrors.ChangedConcurrently);
        }

        return ShopContactsResponse.From(settings);
    }
}

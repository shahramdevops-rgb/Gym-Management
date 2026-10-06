using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.UpdateSmsSettings;

/// <summary>
/// The Owner saves the SMS settings page (BUSINESS_RULES.md §10 <i>SMS settings</i>). A change
/// applies from the next run (each kind's run is moved to its new time, or removed when the kind is
/// off); the audit log keeps what the settings were before.
/// </summary>
/// <remarks>
/// Same two concurrency layers as every edit: the client's <c>Version</c> refuses an edit made on
/// stale settings, and <c>xmin</c> refuses a save that races another one.
/// </remarks>
public sealed class UpdateSmsSettingsHandler(IAppDbContext db, IPhoneNormalizer phones, ISmsRunSchedule schedule)
{
    public async Task<Result<SmsSettingsResponse>> Handle(UpdateSmsSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The members' phone rules (§2): any digits, Iranian mobile only, kept as E.164.
        string? ownerPhone = null;
        if (!string.IsNullOrWhiteSpace(command.OwnerPhone))
        {
            var phone = phones.Normalize(command.OwnerPhone);
            if (phone.IsFailure)
            {
                return Result.Failure<SmsSettingsResponse>(phone.Error);
            }

            ownerPhone = phone.Value;
        }

        var settings = await SmsSettingsRow.ForUpdateAsync(db, cancellationToken);

        if (settings.Version != command.Version)
        {
            return Result.Failure<SmsSettingsResponse>(SmsSettingsErrors.ChangedConcurrently);
        }

        var updated = settings.Update(
            command.Enabled,
            command.SubscriptionExpiring,
            command.LowSessions,
            command.Birthday,
            command.PayableDue,
            ownerPhone);
        if (updated.IsFailure)
        {
            return Result.Failure<SmsSettingsResponse>(updated.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SmsSettingsResponse>(SmsSettingsErrors.ChangedConcurrently);
        }

        // After the save, so a refused save moves no run. The runs follow the saved times from the next one.
        schedule.Apply(settings);

        return SmsSettingsResponse.From(settings);
    }
}

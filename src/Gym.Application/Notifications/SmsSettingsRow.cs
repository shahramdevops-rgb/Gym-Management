using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications;

/// <summary>
/// Reading the one SMS settings row, in one place, for the settings page and the daily jobs.
/// </summary>
/// <remarks>
/// The row always exists: the migration seeds it and nothing deletes it. A missing row is a broken
/// database, not a business answer, so <c>SingleAsync</c> throws rather than returning an error.
/// </remarks>
public static class SmsSettingsRow
{
    /// <summary>Tracked, for the edit.</summary>
    public static Task<SmsSettings> ForUpdateAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.SmsSettings.SingleAsync(settings => settings.Id == SmsSettings.TheId, cancellationToken);
    }

    /// <summary>Not tracked, for reading.</summary>
    public static Task<SmsSettings> CurrentAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.SmsSettings.AsNoTracking().SingleAsync(settings => settings.Id == SmsSettings.TheId, cancellationToken);
    }
}

using Gym.Application.Common;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Shop;

/// <summary>Reading the one shop settings row, in one place.</summary>
/// <remarks>
/// The row always exists: the migration seeds it and nothing deletes it. A missing row is a broken
/// database, not a business answer, so <c>SingleAsync</c> throws rather than returning an error.
/// </remarks>
public static class ShopSettingsRow
{
    /// <summary>Tracked, for the edit.</summary>
    public static Task<ShopSettings> ForUpdateAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.ShopSettings.SingleAsync(settings => settings.Id == ShopSettings.TheId, cancellationToken);
    }

    /// <summary>Not tracked, for reading.</summary>
    public static Task<ShopSettings> CurrentAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.ShopSettings.AsNoTracking().SingleAsync(settings => settings.Id == ShopSettings.TheId, cancellationToken);
    }
}

using Gym.Application.Common;
using Gym.Domain.Pricing;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Pricing;

/// <summary>
/// Reading the one price list, in one place, for every use case that sells or shows a price.
/// </summary>
/// <remarks>
/// The row always exists: the migration seeds it and nothing deletes it. A missing row is a broken
/// database, not a business answer, so <c>SingleAsync</c> throws rather than returning an error.
/// </remarks>
public static class PriceLists
{
    /// <summary>Tracked, for the edit.</summary>
    public static Task<PriceList> ForUpdateAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.PriceLists.SingleAsync(prices => prices.Id == PriceList.TheId, cancellationToken);
    }

    /// <summary>Not tracked, for reading a price or selling at it.</summary>
    public static Task<PriceList> CurrentAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.PriceLists.AsNoTracking().SingleAsync(prices => prices.Id == PriceList.TheId, cancellationToken);
    }
}

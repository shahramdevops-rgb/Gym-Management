using Gym.Domain.Pricing;

namespace Gym.Application.Pricing;

/// <param name="SessionPrice">One session of a plan; <c>null</c> until the Owner sets it.</param>
/// <param name="SingleVisitPrice">One single-session visit; <c>null</c> until the Owner sets it.</param>
/// <param name="Version">Sent back with an edit, so an edit made on stale prices is refused.</param>
public sealed record PricesResponse(decimal? SessionPrice, decimal? SingleVisitPrice, uint Version)
{
    public static PricesResponse From(PriceList prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        return new PricesResponse(prices.SessionPrice, prices.SingleVisitPrice, prices.Version);
    }
}

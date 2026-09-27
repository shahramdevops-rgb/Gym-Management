using Gym.Application.Common;

namespace Gym.Application.Pricing.GetPrices;

/// <summary>
/// The gym's two prices (BUSINESS_RULES.md §3 <i>Prices</i>). Owner and Staff: the desk sells at them
/// and the sale form shows the price before it is confirmed.
/// </summary>
public sealed class GetPricesHandler(IAppDbContext db)
{
    public async Task<PricesResponse> Handle(CancellationToken cancellationToken) =>
        PricesResponse.From(await PriceLists.CurrentAsync(db, cancellationToken));
}

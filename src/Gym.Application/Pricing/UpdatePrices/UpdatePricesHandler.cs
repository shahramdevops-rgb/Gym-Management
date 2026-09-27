using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Pricing;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Pricing.UpdatePrices;

/// <summary>
/// The Owner sets the gym's two prices (BUSINESS_RULES.md §3 <i>Prices</i>). Past sales keep their
/// own price, and the audit log keeps what the prices were before.
/// </summary>
/// <remarks>
/// Same two concurrency layers as every edit: the client's <c>Version</c> refuses an edit made on
/// stale prices, and <c>xmin</c> refuses a save that races another one.
/// </remarks>
public sealed class UpdatePricesHandler(IAppDbContext db)
{
    public async Task<Result<PricesResponse>> Handle(UpdatePricesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var prices = await PriceLists.ForUpdateAsync(db, cancellationToken);

        if (prices.Version != command.Version)
        {
            return Result.Failure<PricesResponse>(PricingErrors.ChangedConcurrently);
        }

        var updated = prices.Update(command.SessionPrice, command.SingleVisitPrice);
        if (updated.IsFailure)
        {
            return Result.Failure<PricesResponse>(updated.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PricesResponse>(PricingErrors.ChangedConcurrently);
        }

        return PricesResponse.From(prices);
    }
}

using Gym.Application.History.ListSales;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.SalesTotals;

/// <summary>
/// The totals row under a sales section (BUSINESS_RULES.md §12 <i>Totals in the history</i>, roadmap
/// 6.5.32). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// The sales come from <see cref="SaleRows"/>, the same query the list pages through, and are
/// summed by the database in one statement: adding them up here would mean loading every sale of
/// the range.
/// </remarks>
public sealed class SalesTotalsHandler(SaleRows saleRows)
{
    public async Task<SalesTotalsResponse> Handle(SalesTotalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A cancelled or voided sale owes nothing and its money came back as a refund (§5).
        // Grouping on a constant turns the three sums into one SELECT; no sale means no group.
        var totals = await saleRows.Matching(query)
            .Where(sale => sale.UndoneAt == null)
            .GroupBy(sale => 1)
            .Select(sales => new SalesTotalsResponse(
                sales.Sum(sale => sale.Amount),
                sales.Sum(sale => sale.NetPaid),
                // Never below zero per sale, as a member's debt (§5): an overpaid sale owes nothing
                // and does not cancel out what another still owes.
                sales.Sum(sale => sale.NetPaid < sale.Amount ? sale.Amount - sale.NetPaid : 0m)))
            // Single, not First: there is one group or none, and EF warns about a First with no order
            // (task 11.3, production logs).
            .SingleOrDefaultAsync(cancellationToken);

        return totals ?? new SalesTotalsResponse(0m, 0m, 0m);
    }
}

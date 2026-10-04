using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetTopCafeProducts;

/// <summary>
/// The cafe's best sellers over a range, by quantity (BUSINESS_RULES.md §12 <i>Operational
/// reports</i>, roadmap 9.2). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// Every order of the range counts, a walk-in's and a guest's included: the question is what sells,
/// not who bought it. A cancelled order was never a sale (§8). The order's own business date
/// (<c>OrderedOn</c>) is the day, as in the history (§12 <i>Sales in the history</i>).
/// </remarks>
public sealed class GetTopCafeProductsHandler(IAppDbContext db)
{
    /// <summary>How many products the list holds.</summary>
    public const int Count = 10;

    public async Task<IReadOnlyList<TopCafeProductResponse>> Handle(
        GetTopCafeProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var firstDay = query.From!.Value;
        var lastDay = query.To!.Value;

        var top = await (
                from item in db.CafeOrderItems.AsNoTracking()
                join placed in db.CafeOrders.AsNoTracking() on item.OrderId equals placed.Id
                where placed.CancelledAt == null && placed.OrderedOn >= firstDay && placed.OrderedOn <= lastDay
                group item by item.ProductId into sold
                select new
                {
                    ProductId = sold.Key,
                    Quantity = sold.Sum(item => item.Quantity),
                    Amount = sold.Sum(item => item.LineTotal),
                })
            .OrderByDescending(row => row.Quantity)
            .ThenByDescending(row => row.Amount)
            .ThenBy(row => row.ProductId)
            .Take(Count)
            .ToListAsync(cancellationToken);

        // A product is switched off, never deleted (§8), so every id still has its row and its name.
        var ids = top.Select(row => row.ProductId).ToList();
        var names = await db.Products
            .AsNoTracking()
            .Where(product => ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.Name, cancellationToken);

        return top
            .Select(row => new TopCafeProductResponse(
                row.ProductId, names.GetValueOrDefault(row.ProductId) ?? string.Empty, row.Quantity, row.Amount))
            .ToList();
    }
}

using Gym.Application.Common;
using Gym.Application.History.ListSales;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetReceivables;

/// <summary>
/// What is owed to the gym right now, by age (BUSINESS_RULES.md §12 <i>Receivables</i>, roadmap
/// 9.1). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// The sales come from <see cref="SaleRows"/> with no range and no member: the same query, and the
/// same "never below zero per sale", as the history's «مانده». So the total here is exactly what
/// «همهٔ فروش‌ها» with no dates would show as owed.
/// </remarks>
public sealed class GetReceivablesHandler(SaleRows saleRows, IGymCalendar calendar)
{
    public async Task<ReceivablesResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var weekStart = calendar.StartOfDayUtc(today.AddDays(-7));
        var monthStart = calendar.StartOfDayUtc(today.AddDays(-ReportThresholds.OldDebtDays));

        // A cancelled or voided sale owes nothing (§5). Grouping on a constant turns the three sums
        // into one SELECT; nothing owed means no group.
        var ages = await saleRows.Matching(new EverySale())
            .Where(sale => sale.UndoneAt == null && sale.NetPaid < sale.Amount)
            .GroupBy(sale => 1)
            .Select(sales => new
            {
                Recent = sales.Sum(sale => sale.SoldAt >= weekStart ? sale.Amount - sale.NetPaid : 0m),
                Month = sales.Sum(sale =>
                    sale.SoldAt < weekStart && sale.SoldAt >= monthStart ? sale.Amount - sale.NetPaid : 0m),
                Older = sales.Sum(sale => sale.SoldAt < monthStart ? sale.Amount - sale.NetPaid : 0m),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return ages is null
            ? new ReceivablesResponse(0m, 0m, 0m, 0m)
            : new ReceivablesResponse(ages.Recent + ages.Month + ages.Older, ages.Recent, ages.Month, ages.Older);
    }

    /// <summary>No range, no member, every kind: everything ever sold.</summary>
    private sealed record EverySale(
        DateOnly? From = null,
        DateOnly? To = null,
        Guid? MemberId = null,
        SaleSource? Source = null,
        SalePaidFilter? Paid = null) : ISalesFilter;
}

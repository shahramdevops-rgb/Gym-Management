using Gym.Application.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.ListPayables;

/// <summary>
/// The register of cheques and instalments (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// Owner only.
/// </summary>
/// <remarks>
/// Pending ones read the earliest date first, because the next one to pay is the question. Every
/// other list is a record, the latest date first, like the expenses.
/// </remarks>
public sealed class ListPayablesHandler(IAppDbContext db)
{
    public async Task<PayableListResponse> Handle(ListPayablesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pendingByKind = await db.Payables.AsNoTracking()
            .Where(payable => payable.PaidAt == null && payable.CancelledAt == null)
            .GroupBy(payable => payable.Kind)
            .Select(group => new { Kind = group.Key, Total = group.Sum(payable => payable.Amount) })
            .ToDictionaryAsync(row => row.Kind, row => row.Total, cancellationToken);
        var pendingCheques = pendingByKind.GetValueOrDefault(PayableKind.Cheque);
        var pendingInstallments = pendingByKind.GetValueOrDefault(PayableKind.Installment);

        var payables = query.Status switch
        {
            PayableStatus.Pending => db.Payables.Where(payable => payable.PaidAt == null && payable.CancelledAt == null),
            PayableStatus.Paid => db.Payables.Where(payable => payable.PaidAt != null),
            PayableStatus.Cancelled => db.Payables.Where(payable => payable.CancelledAt != null),
            _ => db.Payables,
        };
        if (query.Kind is { } kind)
        {
            payables = payables.Where(payable => payable.Kind == kind);
        }

        payables = payables.AsNoTracking();

        var totalCount = await payables.CountAsync(cancellationToken);

        // Id breaks the last tie so paging never repeats or skips a row.
        IOrderedQueryable<Payable> ordered = query.Status == PayableStatus.Pending
            ? payables.OrderBy(payable => payable.DueDate).ThenBy(payable => payable.Id)
            : payables.OrderByDescending(payable => payable.DueDate).ThenByDescending(payable => payable.Id);

        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = await PayableResponses.ForAsync(db, rows, cancellationToken);

        return new PayableListResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            pendingCheques + pendingInstallments,
            pendingCheques,
            pendingInstallments);
    }
}

using Gym.Application.Common;
using Gym.Domain.Cheques;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cheques.ListCheques;

/// <summary>
/// The cheque register (BUSINESS_RULES.md §9 <i>Cheques</i>). Owner only.
/// </summary>
/// <remarks>
/// Pending cheques read the earliest date first, because the next one to pay is the question.
/// Every other list is a record, the latest date first, like the expenses.
/// </remarks>
public sealed class ListChequesHandler(IAppDbContext db)
{
    public async Task<ChequeListResponse> Handle(ListChequesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pendingTotal = await db.Cheques.AsNoTracking()
            .Where(cheque => cheque.PassedAt == null && cheque.CancelledAt == null)
            .SumAsync(cheque => cheque.Amount, cancellationToken);

        var cheques = query.Status switch
        {
            ChequeStatus.Pending => db.Cheques.Where(cheque => cheque.PassedAt == null && cheque.CancelledAt == null),
            ChequeStatus.Passed => db.Cheques.Where(cheque => cheque.PassedAt != null),
            ChequeStatus.Cancelled => db.Cheques.Where(cheque => cheque.CancelledAt != null),
            _ => db.Cheques,
        };
        cheques = cheques.AsNoTracking();

        var totalCount = await cheques.CountAsync(cancellationToken);

        // Id breaks the last tie so paging never repeats or skips a row.
        IOrderedQueryable<Cheque> ordered = query.Status == ChequeStatus.Pending
            ? cheques.OrderBy(cheque => cheque.DueDate).ThenBy(cheque => cheque.Id)
            : cheques.OrderByDescending(cheque => cheque.DueDate).ThenByDescending(cheque => cheque.Id);

        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ChequeResponse.From).ToList();

        return new ChequeListResponse(items, query.Page, query.PageSize, totalCount, pendingTotal);
    }
}

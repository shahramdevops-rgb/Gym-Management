using Gym.Application.Common.Paging;
using Gym.Domain.Payables;

namespace Gym.Application.Payables.ListPayables;

/// <summary>
/// Bound from the query string: <c>GET /api/payables?status=Pending&amp;kind=Installment&amp;page=1</c>.
/// </summary>
/// <param name="Status">One status only; omitted means every status.</param>
/// <param name="Kind">Cheques or instalments only; omitted means both.</param>
public sealed record ListPayablesQuery(
    PayableStatus? Status = null,
    PayableKind? Kind = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

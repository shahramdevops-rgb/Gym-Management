using Gym.Application.Common.Paging;

namespace Gym.Application.Cheques.ListCheques;

/// <summary>
/// Bound from the query string: <c>GET /api/cheques?status=Pending&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="Status">One status only; omitted means every cheque.</param>
public sealed record ListChequesQuery(
    ChequeStatus? Status = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

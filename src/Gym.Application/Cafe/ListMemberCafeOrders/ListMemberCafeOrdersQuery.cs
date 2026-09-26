using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListMemberCafeOrders;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/members/{memberId}/cafe-orders?from=2026-09-01&amp;to=2026-09-30&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="From">Inclusive, by the order's business date. <c>null</c> means no lower bound.</param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
public sealed record ListMemberCafeOrdersQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

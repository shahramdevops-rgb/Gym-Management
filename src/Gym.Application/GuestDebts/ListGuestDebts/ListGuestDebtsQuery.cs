using Gym.Application.Common.Paging;

namespace Gym.Application.GuestDebts.ListGuestDebts;

/// <summary>
/// Bound from the query string: <c>GET /api/guest-debts?page=1&amp;pageSize=20</c>. No filters: the
/// list is short, and every row on it is work for the desk (BUSINESS_RULES.md §7 <i>Guest visit</i>).
/// </summary>
public sealed record ListGuestDebtsQuery(
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

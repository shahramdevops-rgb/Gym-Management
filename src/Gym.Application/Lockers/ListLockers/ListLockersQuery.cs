using Gym.Application.Common.Paging;

namespace Gym.Application.Lockers.ListLockers;

/// <summary>Bound from the query string: <c>GET /api/lockers?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListLockersQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);

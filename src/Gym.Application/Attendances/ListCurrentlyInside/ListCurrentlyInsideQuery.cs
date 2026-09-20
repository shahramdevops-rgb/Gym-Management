using Gym.Application.Common.Paging;

namespace Gym.Application.Attendances.ListCurrentlyInside;

/// <summary>Bound from the query string: <c>GET /api/attendance/currently-inside?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListCurrentlyInsideQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);

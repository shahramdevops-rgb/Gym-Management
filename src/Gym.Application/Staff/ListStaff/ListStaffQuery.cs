using Gym.Application.Common.Paging;

namespace Gym.Application.Staff.ListStaff;

/// <summary>Bound from the query string: <c>GET /api/staff?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListStaffQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);

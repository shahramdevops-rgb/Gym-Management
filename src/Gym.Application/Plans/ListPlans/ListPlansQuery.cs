using Gym.Application.Common.Paging;

namespace Gym.Application.Plans.ListPlans;

/// <summary>Bound from the query string: <c>GET /api/plans?isActive=true&amp;page=1&amp;pageSize=20</c>.</summary>
/// <param name="IsActive">
/// Only active (<c>true</c>) or inactive (<c>false</c>) plans; omitted means both. Selling a
/// subscription will ask for active plans only.
/// </param>
public sealed record ListPlansQuery(
    bool? IsActive = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

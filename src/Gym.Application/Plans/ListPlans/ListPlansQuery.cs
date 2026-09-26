using Gym.Application.Common.Paging;
using Gym.Domain.Plans;

namespace Gym.Application.Plans.ListPlans;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/plans?kind=SingleSession&amp;isActive=true&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="IsActive">
/// Only active (<c>true</c>) or inactive (<c>false</c>) plans; omitted means both. Selling a
/// subscription will ask for active plans only.
/// </param>
/// <param name="Kind">
/// Only plans of this kind; omitted means both. The entry screen asks for
/// <see cref="PlanKind.SingleSession"/> to find the one plan a single visit is sold from
/// (BUSINESS_RULES.md §3), which it cannot do by paging: there is exactly one of them and it can
/// sit on any page. It asks without an <see cref="IsActive"/> filter on purpose, so it can tell
/// "the Owner has not created it yet" from "it is switched off" and say which.
/// </param>
public sealed record ListPlansQuery(
    bool? IsActive = null,
    PlanKind? Kind = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

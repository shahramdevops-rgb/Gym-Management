using Gym.Application.Common.Paging;

namespace Gym.Application.Attendances.ListMemberAttendance;

/// <summary>
/// Bound from the query string: <c>GET /api/members/{memberId}/attendance?from=2026-09-01&amp;to=2026-09-30&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="From">Inclusive. <c>null</c> means no lower bound (BUSINESS_RULES.md §12: date ranges are inclusive).</param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
public sealed record ListMemberAttendanceQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

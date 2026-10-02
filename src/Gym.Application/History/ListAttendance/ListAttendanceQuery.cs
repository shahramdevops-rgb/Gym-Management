using Gym.Application.Common.Paging;

namespace Gym.Application.History.ListAttendance;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/attendance?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with the day the visit began in the gym's time zone. <c>null</c> means no
/// lower bound (BUSINESS_RULES.md §12: date ranges are inclusive, in the gym's time zone).
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="MemberId">One member's visits; omitted means everyone's.</param>
public sealed record ListAttendanceQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

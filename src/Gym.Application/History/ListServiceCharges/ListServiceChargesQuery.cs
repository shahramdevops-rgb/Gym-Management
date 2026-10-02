using Gym.Application.Common.Paging;

namespace Gym.Application.History.ListServiceCharges;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/service-charges?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with the charge's business date (<c>ChargedOn</c>). <c>null</c> means no
/// lower bound (BUSINESS_RULES.md §12: date ranges are inclusive, in the gym's time zone).
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="MemberId">One member's charges; omitted means everyone's.</param>
public sealed record ListServiceChargesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

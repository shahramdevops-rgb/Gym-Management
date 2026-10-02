using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListCafeOrders;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/cafe/orders?memberId=...&amp;from=2026-09-01&amp;to=2026-09-30&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="MemberId">One member's purchases; omitted means everyone's, walk-ins included.</param>
/// <param name="From">
/// Inclusive, compared with the order's business date. <c>null</c> means no lower bound
/// (BUSINESS_RULES.md §12: date ranges are inclusive, in the gym's time zone).
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="AttendanceId">
/// What was bought during one visit — what check-out shows the member before they leave.
/// </param>
/// <param name="UnpaidGuest">
/// Only orders on a guest's visit that still owe money and are not cancelled: what the nightly job
/// left behind when it closed a guest's visit (BUSINESS_RULES.md §7 <i>Guest visit</i>), shown as
/// «پرداخت‌نشده — مهمان».
/// </param>
public sealed record ListCafeOrdersQuery(
    Guid? MemberId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? AttendanceId = null,
    bool? UnpaidGuest = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

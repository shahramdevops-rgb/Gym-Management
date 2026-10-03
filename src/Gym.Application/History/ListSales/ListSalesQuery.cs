using Gym.Application.Common.Paging;

namespace Gym.Application.History.ListSales;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/sales?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;source=Cardio&amp;paid=Unpaid&amp;page=1</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with the day each sale belongs to: a subscription's sale moment
/// (<c>CreatedAt</c>) in the gym's time zone, a charge's <c>ChargedOn</c>, a cafe order's
/// <c>OrderedOn</c> (BUSINESS_RULES.md §12 <i>Sales in the history</i>). <c>null</c> means no lower bound.
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="MemberId">One member's sales; omitted means everyone's, walk-in and guest cafe orders included.</param>
/// <param name="Source">Only one kind of sale; omitted means all of them («همهٔ فروش‌ها»).</param>
/// <param name="Paid">Only fully paid, or only still owed; omitted means every sale, cancelled and voided included.</param>
public sealed record ListSalesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    SaleSource? Source = null,
    SalePaidFilter? Paid = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

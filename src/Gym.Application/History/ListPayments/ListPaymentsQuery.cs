using Gym.Application.Common.Paging;
using Gym.Domain.Payments;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/payments?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;method=Cash&amp;source=CafeOrder&amp;page=1</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with the day the money was taken (<c>PaidAt</c>) in the gym's time zone.
/// <c>null</c> means no lower bound, which only the Owner may ask for (BUSINESS_RULES.md §12
/// <i>History</i>).
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="MemberId">
/// Money for one member's subscriptions, هوازی and cafe orders. Omitted means everyone's, walk-in
/// cafe sales included.
/// </param>
/// <param name="Method">Only this payment method.</param>
/// <param name="Source">Only money for this kind of item: subscription, هوازی or cafe.</param>
public sealed record ListPaymentsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    PaymentMethod? Method = null,
    PaymentTargetKind? Source = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

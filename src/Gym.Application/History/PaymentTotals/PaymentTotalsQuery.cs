using Gym.Application.History.ListPayments;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.History.PaymentTotals;

/// <summary>
/// Bound from the query string, with the payments list's own filters and no page:
/// <c>GET /api/payments/totals?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;method=Cash&amp;source=CafeOrder</c>.
/// What each filter means is on <see cref="ListPaymentsQuery"/>.
/// </summary>
public sealed record PaymentTotalsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    PaymentMethod? Method = null,
    PaymentTargetKind? Source = null,
    ServiceChargeKind? ServiceKind = null) : IPaymentsFilter;

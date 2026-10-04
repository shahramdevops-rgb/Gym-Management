using Gym.Application.History.ListSales;

namespace Gym.Application.History.SalesTotals;

/// <summary>
/// Bound from the query string, with the sales list's own filters and no page:
/// <c>GET /api/sales/totals?from=2026-09-29&amp;to=2026-10-02&amp;memberId=...&amp;source=Cardio&amp;paid=Unpaid</c>.
/// What each filter means is on <see cref="ISalesFilter"/>.
/// </summary>
public sealed record SalesTotalsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? MemberId = null,
    SaleSource? Source = null,
    SalePaidFilter? Paid = null) : ISalesFilter;

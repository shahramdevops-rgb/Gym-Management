namespace Gym.Application.Reports.GetTopCafeProducts;

/// <summary>
/// Bound from the query string: <c>GET /api/reports/cafe-products?from=2026-09-23&amp;to=2026-10-04</c>
/// (BUSINESS_RULES.md §12 <i>Operational reports</i>).
/// </summary>
public sealed record GetTopCafeProductsQuery(DateOnly? From = null, DateOnly? To = null) : IReportRangeQuery;

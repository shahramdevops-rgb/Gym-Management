namespace Gym.Application.Reports.GetFinancialReport;

/// <summary>
/// Bound from the query string: <c>GET /api/reports/financial?from=2026-09-23&amp;to=2026-10-04</c>.
/// Both dates are inclusive business dates in the gym's time zone (BUSINESS_RULES.md §12
/// <i>Financial report</i>).
/// </summary>
public sealed record GetFinancialReportQuery(DateOnly? From = null, DateOnly? To = null) : IReportRangeQuery;

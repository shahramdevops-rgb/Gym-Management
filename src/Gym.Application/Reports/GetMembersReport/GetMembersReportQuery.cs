namespace Gym.Application.Reports.GetMembersReport;

/// <summary>
/// Bound from the query string: <c>GET /api/reports/members?from=2026-09-23&amp;to=2026-10-04</c>
/// (BUSINESS_RULES.md §12 <i>Operational reports</i>).
/// </summary>
public sealed record GetMembersReportQuery(DateOnly? From = null, DateOnly? To = null) : IReportRangeQuery;

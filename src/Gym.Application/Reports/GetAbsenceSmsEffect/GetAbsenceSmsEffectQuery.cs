namespace Gym.Application.Reports.GetAbsenceSmsEffect;

/// <summary>
/// Bound from the query string: <c>GET /api/reports/absence-sms?from=2026-10-01&amp;to=2026-10-26</c>
/// (BUSINESS_RULES.md §12 <i>The absence SMS's effect</i>).
/// </summary>
public sealed record GetAbsenceSmsEffectQuery(DateOnly? From = null, DateOnly? To = null) : IReportRangeQuery;

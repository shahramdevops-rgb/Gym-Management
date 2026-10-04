namespace Gym.Application.Reports;

/// <summary>
/// A report over a range of the gym's days: <c>?from=2026-09-23&amp;to=2026-10-04</c>, both
/// inclusive, in the gym's time zone (BUSINESS_RULES.md §12).
/// </summary>
/// <remarks>
/// Nullable only so that a missing date reaches <see cref="ReportRangeValidator{T}"/> and gets its
/// own error code (<c>Reports.DateRangeRequired</c>) rather than the binder's generic refusal. A
/// handler runs only once both are there.
/// </remarks>
public interface IReportRangeQuery
{
    DateOnly? From { get; }

    DateOnly? To { get; }
}

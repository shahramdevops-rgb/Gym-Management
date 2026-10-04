using Gym.Domain.Common;

namespace Gym.Application.Reports;

/// <summary>The reports' refusals (BUSINESS_RULES.md §12 <i>Financial report</i>).</summary>
public static class ReportErrors
{
    public static readonly Error DateRangeRequired = Error.Validation(
        "Reports.DateRangeRequired",
        "A report needs both the start and the end of its date range.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "Reports.InvalidDateRange",
        "The start of the date range is after its end.");

    public static readonly Error RangeTooLong = Error.Validation(
        "Reports.RangeTooLong",
        "A report covers at most 366 days.");
}

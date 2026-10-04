using FluentValidation;

namespace Gym.Application.Reports.GetFinancialReport;

/// <summary>
/// A range is required, runs forward, and covers at most <see cref="MaxDays"/> days
/// (BUSINESS_RULES.md §12 <i>Financial report</i>).
/// </summary>
public sealed class GetFinancialReportValidator : AbstractValidator<GetFinancialReportQuery>
{
    /// <summary>A Jalali leap year: "this year" always fits, and no request reads more than one.</summary>
    public const int MaxDays = 366;

    public GetFinancialReportValidator()
    {
        RuleFor(query => query.From)
            .NotNull()
            .WithErrorCode(ReportErrors.DateRangeRequired.Code)
            .WithMessage(ReportErrors.DateRangeRequired.Description);

        RuleFor(query => query.To)
            .NotNull()
            .WithErrorCode(ReportErrors.DateRangeRequired.Code)
            .WithMessage(ReportErrors.DateRangeRequired.Description);

        // The handler reads the range before this one as well, and the day after To as an
        // exclusive bound; both must stay inside what DateOnly can hold, or AddDays throws.
        RuleFor(query => query)
            .Must(query => query.From!.Value <= query.To!.Value &&
                query.From.Value >= DateOnly.MinValue.AddDays(MaxDays) &&
                query.To.Value < DateOnly.MaxValue)
            .When(query => query.From is not null && query.To is not null)
            .WithErrorCode(ReportErrors.InvalidDateRange.Code)
            .WithMessage(ReportErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(GetFinancialReportQuery.To));

        RuleFor(query => query)
            .Must(query => query.To!.Value.DayNumber - query.From!.Value.DayNumber + 1 <= MaxDays)
            .When(query => query.From is not null && query.To is not null && query.From <= query.To)
            .WithErrorCode(ReportErrors.RangeTooLong.Code)
            .WithMessage(ReportErrors.RangeTooLong.Description)
            .OverridePropertyName(nameof(GetFinancialReportQuery.To));
    }
}

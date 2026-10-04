using FluentValidation;

namespace Gym.Application.Reports;

/// <summary>
/// Every report's range: required, runs forward, and covers at most <see cref="ReportRange.MaxDays"/>
/// days (BUSINESS_RULES.md §12 <i>Financial report</i>). Each report's validator derives from this
/// one, so they all refuse a range the same way, with the same codes.
/// </summary>
public abstract class ReportRangeValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : IReportRangeQuery
{
    protected ReportRangeValidator()
    {
        RuleFor(query => query.From)
            .NotNull()
            .WithErrorCode(ReportErrors.DateRangeRequired.Code)
            .WithMessage(ReportErrors.DateRangeRequired.Description);

        RuleFor(query => query.To)
            .NotNull()
            .WithErrorCode(ReportErrors.DateRangeRequired.Code)
            .WithMessage(ReportErrors.DateRangeRequired.Description);

        // A report may read the range before this one as well, and the day after To as an
        // exclusive bound; both must stay inside what DateOnly can hold, or AddDays throws.
        RuleFor(query => query)
            .Must(query => query.From!.Value <= query.To!.Value &&
                query.From.Value >= DateOnly.MinValue.AddDays(ReportRange.MaxDays) &&
                query.To.Value < DateOnly.MaxValue.AddDays(-ReportRange.MaxDays))
            .When(query => query.From is not null && query.To is not null)
            .WithErrorCode(ReportErrors.InvalidDateRange.Code)
            .WithMessage(ReportErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(IReportRangeQuery.To));

        RuleFor(query => query)
            .Must(query => query.To!.Value.DayNumber - query.From!.Value.DayNumber + 1 <= ReportRange.MaxDays)
            .When(query => query.From is not null && query.To is not null && query.From <= query.To)
            .WithErrorCode(ReportErrors.RangeTooLong.Code)
            .WithMessage(ReportErrors.RangeTooLong.Description)
            .OverridePropertyName(nameof(IReportRangeQuery.To));
    }
}

/// <summary>The limits every report's range shares.</summary>
public static class ReportRange
{
    /// <summary>A Jalali leap year: "this year" always fits, and no request reads more than one.</summary>
    public const int MaxDays = 366;
}

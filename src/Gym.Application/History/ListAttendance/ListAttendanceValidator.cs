using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Attendances;

namespace Gym.Application.History.ListAttendance;

public sealed class ListAttendanceValidator : AbstractValidator<ListAttendanceQuery>
{
    public ListAttendanceValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        // One day before DateOnly.MaxValue: the handler computes an exclusive upper bound with
        // To.AddDays(1), which throws for MaxValue itself rather than returning a Result.
        RuleFor(query => query.To)
            .Must(to => to is null || to <= DateOnly.MaxValue.AddDays(-1))
            .WithErrorCode(AttendanceErrors.InvalidDateRange.Code)
            .WithMessage(AttendanceErrors.InvalidDateRange.Description);

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(AttendanceErrors.InvalidDateRange.Code)
            .WithMessage(AttendanceErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListAttendanceQuery.To));
    }
}

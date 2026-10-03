using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Payments;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// The shape of the request only. Whether Staff may reach that far back depends on who is asking
/// and on today, so the handler checks it (<see cref="PaymentHistoryWindow"/>).
/// </summary>
public sealed class ListPaymentsValidator : AbstractValidator<ListPaymentsQuery>
{
    public ListPaymentsValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        // An integer that names no method or source would otherwise filter everything out.
        RuleFor(query => query.Method)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.MethodInvalid.Code)
            .WithMessage(PaymentErrors.MethodInvalid.Description);

        RuleFor(query => query.Source)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        RuleFor(query => query.ServiceKind)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        // One day before DateOnly.MaxValue: the handler computes an exclusive upper bound with
        // To.AddDays(1), which throws for MaxValue itself rather than returning a Result.
        RuleFor(query => query.To)
            .Must(to => to is null || to <= DateOnly.MaxValue.AddDays(-1))
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description);

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListPaymentsQuery.To));
    }
}

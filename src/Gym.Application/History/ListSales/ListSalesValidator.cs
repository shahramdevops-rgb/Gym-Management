using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Payments;

namespace Gym.Application.History.ListSales;

public sealed class ListSalesValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        // An integer that names no source or choice would otherwise filter everything out.
        RuleFor(query => query.Source)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        RuleFor(query => query.Paid)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidPaidFilter.Code)
            .WithMessage(PaymentErrors.InvalidPaidFilter.Description);

        // One day before DateOnly.MaxValue: the handler computes an exclusive upper bound for the
        // subscriptions with To.AddDays(1), which throws for MaxValue itself.
        RuleFor(query => query.To)
            .Must(to => to is null || to <= DateOnly.MaxValue.AddDays(-1))
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description);

        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ListSalesQuery.To));
    }
}

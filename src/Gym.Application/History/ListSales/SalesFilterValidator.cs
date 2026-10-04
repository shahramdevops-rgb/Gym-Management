using FluentValidation;

using Gym.Domain.Payments;

namespace Gym.Application.History.ListSales;

/// <summary>
/// The rules every sales request shares, whether it asks for a page or for the totals. Each
/// request's own validator takes them in with <c>Include</c>.
/// </summary>
public sealed class SalesFilterValidator : AbstractValidator<ISalesFilter>
{
    public SalesFilterValidator()
    {
        // An integer that names no source or choice would otherwise filter everything out.
        RuleFor(filter => filter.Source)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        RuleFor(filter => filter.Paid)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidPaidFilter.Code)
            .WithMessage(PaymentErrors.InvalidPaidFilter.Description);

        // One day before DateOnly.MaxValue: SaleRows computes an exclusive upper bound for the
        // subscriptions with To.AddDays(1), which throws for MaxValue itself.
        RuleFor(filter => filter.To)
            .Must(to => to is null || to <= DateOnly.MaxValue.AddDays(-1))
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description);

        RuleFor(filter => filter)
            .Must(filter => filter.From is null || filter.To is null || filter.From <= filter.To)
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(ISalesFilter.To));
    }
}

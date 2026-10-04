using FluentValidation;

using Gym.Domain.Payments;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// The rules every payments request shares, whether it asks for a page or for the totals. Each
/// request's own validator takes them in with <c>Include</c>.
/// </summary>
public sealed class PaymentsFilterValidator : AbstractValidator<IPaymentsFilter>
{
    public PaymentsFilterValidator()
    {
        // An integer that names no method or source would otherwise filter everything out.
        RuleFor(filter => filter.Method)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.MethodInvalid.Code)
            .WithMessage(PaymentErrors.MethodInvalid.Description);

        RuleFor(filter => filter.Source)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        RuleFor(filter => filter.ServiceKind)
            .IsInEnum()
            .WithErrorCode(PaymentErrors.InvalidSource.Code)
            .WithMessage(PaymentErrors.InvalidSource.Description);

        // One day before DateOnly.MaxValue: PaymentRows computes an exclusive upper bound with
        // To.AddDays(1), which throws for MaxValue itself rather than returning a Result.
        RuleFor(filter => filter.To)
            .Must(to => to is null || to <= DateOnly.MaxValue.AddDays(-1))
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description);

        RuleFor(filter => filter)
            .Must(filter => filter.From is null || filter.To is null || filter.From <= filter.To)
            .WithErrorCode(PaymentErrors.InvalidDateRange.Code)
            .WithMessage(PaymentErrors.InvalidDateRange.Description)
            .OverridePropertyName(nameof(IPaymentsFilter.To));
    }
}

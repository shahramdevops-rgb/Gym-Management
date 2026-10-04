using FluentValidation;

using Gym.Application.History.ListPayments;

namespace Gym.Application.History.PaymentTotals;

/// <summary>
/// The list's rules. There is no Staff window to check: the totals are the Owner's alone, and the
/// Owner reaches any day (BUSINESS_RULES.md §1).
/// </summary>
public sealed class PaymentTotalsValidator : AbstractValidator<PaymentTotalsQuery>
{
    public PaymentTotalsValidator()
    {
        Include(new PaymentsFilterValidator());
    }
}

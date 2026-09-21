namespace Gym.Domain.Payments;

/// <summary>
/// Calculated from a subscription's price and its net paid amount, never stored
/// (BUSINESS_RULES.md §4 "Payment status"). See <see cref="PaymentStatusCalculator"/>.
/// </summary>
public enum PaymentStatus
{
    Unpaid,
    Partial,
    Paid,
}

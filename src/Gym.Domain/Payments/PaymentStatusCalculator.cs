namespace Gym.Domain.Payments;

/// <summary>
/// BUSINESS_RULES.md §4 "Payment status (calculated)": net paid = payments − refunds.
/// A pure function rather than a method on <c>Subscription</c>, because the subscription itself
/// does not know its payments; the caller sums them (see <c>PaymentLedger</c> in Application).
/// </summary>
public static class PaymentStatusCalculator
{
    public static PaymentStatus Calculate(decimal price, decimal netPaid)
    {
        // Checked before the zero case: a free (Price = 0) subscription owes nothing and is
        // Paid from the start, never Unpaid, even though its net paid is also zero.
        if (netPaid >= price)
        {
            return PaymentStatus.Paid;
        }

        return netPaid <= 0 ? PaymentStatus.Unpaid : PaymentStatus.Partial;
    }
}

using Gym.Application.History.ListPayments;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.PaymentTotals;

/// <summary>
/// The totals row under «پرداخت‌ها» (BUSINESS_RULES.md §12 <i>Totals in the history</i>, roadmap
/// 6.5.32). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// Every payment and refund counts, those on a cancelled or voided item included: the money did
/// move, in and back out. The rows come from <see cref="PaymentRows"/>, the list's own query.
/// </remarks>
public sealed class PaymentTotalsHandler(PaymentRows paymentRows)
{
    public async Task<PaymentTotalsResponse> Handle(PaymentTotalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Grouping on a constant turns both sums into one SELECT; no payment means no group.
        var sums = await paymentRows.Matching(query)
            .GroupBy(payment => 1)
            .Select(payments => new
            {
                Received = payments.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : 0m),
                Refunded = payments.Sum(payment => payment.Kind == PaymentKind.Refund ? payment.Amount : 0m),
            })
            // Single, not First: there is one group or none, and EF warns about a First with no order
            // (task 11.3, production logs).
            .SingleOrDefaultAsync(cancellationToken);

        return sums is null
            ? new PaymentTotalsResponse(0m, 0m, 0m)
            : new PaymentTotalsResponse(sums.Received, sums.Refunded, sums.Received - sums.Refunded);
    }
}

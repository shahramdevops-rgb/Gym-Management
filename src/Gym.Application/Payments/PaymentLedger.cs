using Gym.Application.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments;

/// <summary>
/// BUSINESS_RULES.md §4 "net paid = payments − refunds", in one place so every feature that
/// needs a subscription's payment status (registering a payment now, refunds and history later)
/// computes it the same way.
/// </summary>
public static class PaymentLedger
{
    public static Task<decimal> GetNetPaidAsync(IAppDbContext db, Guid subscriptionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.SubscriptionId == subscriptionId)
            .SumAsync(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount, cancellationToken);
    }
}

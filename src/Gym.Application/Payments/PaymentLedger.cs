using Gym.Application.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments;

/// <summary>
/// BUSINESS_RULES.md §4 "net paid = payments − refunds", in one place so every feature that
/// needs it — a subscription's payment status, a service charge's, the member's debt — computes
/// it the same way.
/// </summary>
/// <remarks>
/// One method per target rather than one taking a nullable pair, because a payment belongs to
/// exactly one thing (§5) and the caller always knows which.
/// </remarks>
public static class PaymentLedger
{
    public static Task<decimal> GetNetPaidAsync(IAppDbContext db, Guid subscriptionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.SubscriptionId == subscriptionId)
            .SumAsync(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount, cancellationToken);
    }

    public static Task<decimal> GetNetPaidForServiceChargeAsync(
        IAppDbContext db, Guid serviceChargeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.ServiceChargeId == serviceChargeId)
            .SumAsync(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount, cancellationToken);
    }
}

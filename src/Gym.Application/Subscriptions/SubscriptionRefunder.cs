using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Subscriptions;

/// <summary>
/// Gives back the money taken against a subscription that is being cancelled (BUSINESS_RULES.md
/// §4 <i>Cancel</i>, roadmap 6.5.37) — the subscription's twin of <c>CafeOrderRefunder</c> and
/// <c>ServiceChargeRefunder</c>. Cancelling is the only way a subscription is refunded (§5).
/// </summary>
/// <remarks>
/// <b>Money goes back the way it came</b>: one refund per payment method still in credit, so cash
/// comes back as cash and a card payment is reversed on the card, and no caller has to ask which
/// method to use.
/// </remarks>
public static class SubscriptionRefunder
{
    /// <summary>
    /// Adds a refund per payment method still in credit for <paramref name="subscription"/>,
    /// carrying its cancellation reason, and returns their total. Nothing is added when nothing was
    /// paid. The caller has cancelled the subscription, holds the member's lock, and saves.
    /// </summary>
    public static async Task<decimal> RefundNetPaidAsync(
        IAppDbContext db, Subscription subscription, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(subscription);

        var netPaidByMethod = await PaymentLedger.GetNetPaidByMethodForSubscriptionAsync(db, subscription.Id, cancellationToken);

        var refunded = 0m;

        foreach (var row in netPaidByMethod)
        {
            var refund = Payment.RegisterRefundForSubscription(
                subscription.Id, row.NetPaid, row.Method, referenceNumber: null, subscription.CancellationReason!, userId, now);

            // The amount comes from rows this application wrote, each already within the money
            // rules, and the reason passed the cancel's own length check, so a failure here is a
            // bug rather than something a user can cause.
            if (refund.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Refunding a cancelled subscription produced an invalid payment: {refund.Error.Code}.");
            }

            db.Payments.Add(refund.Value);
            refunded += row.NetPaid;
        }

        return refunded;
    }
}

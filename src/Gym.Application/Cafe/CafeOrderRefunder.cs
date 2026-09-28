using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Cafe;
using Gym.Domain.Payments;

namespace Gym.Application.Cafe;

/// <summary>
/// Gives back the money taken against a cafe order that is being cancelled (BUSINESS_RULES.md
/// §8) — the cafe's twin of <c>ServiceChargeRefunder</c>. Shared by cancelling an order at the
/// till and cancelling the check-in it was bought on (§7 <i>Cancel check-in</i>).
/// </summary>
/// <remarks>
/// <b>Money goes back the way it came</b>: one refund per payment method still in credit, so cash
/// comes back as cash and a card payment is reversed on the card, and no caller has to ask which
/// method to use.
/// </remarks>
public static class CafeOrderRefunder
{
    /// <summary>
    /// Adds a refund per payment method still in credit for <paramref name="order"/>, carrying
    /// the order's cancel reason, and returns their total. Nothing is added when nothing was paid,
    /// the ordinary case for an order on account. The caller has cancelled the order and saves.
    /// </summary>
    public static async Task<decimal> RefundNetPaidAsync(
        IAppDbContext db, CafeOrder order, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(order);

        var netPaidByMethod = await PaymentLedger.GetNetPaidByMethodForCafeOrderAsync(db, order.Id, cancellationToken);

        var refunded = 0m;

        foreach (var row in netPaidByMethod)
        {
            var refund = Payment.RegisterRefundForCafeOrder(
                order.Id, row.NetPaid, row.Method, referenceNumber: null, order.CancelReason!, userId, now);

            // The amount comes from rows this application wrote, each already within the money
            // rules, so a failure here is a bug rather than something a user can cause.
            if (refund.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Refunding a cancelled cafe order produced an invalid payment: {refund.Error.Code}.");
            }

            db.Payments.Add(refund.Value);
            refunded += row.NetPaid;
        }

        return refunded;
    }
}

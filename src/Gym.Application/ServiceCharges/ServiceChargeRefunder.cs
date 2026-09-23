using Gym.Application.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges;

/// <summary>
/// Gives back the money taken against a charge that is being voided (BUSINESS_RULES.md §5, §7).
/// </summary>
/// <remarks>
/// <para>
/// A voided charge owes nothing, so money already collected for it has to leave the books too —
/// §5 is explicit that there is no wallet and no credit balance, so it cannot simply sit against
/// the member's name. The refund is written in the same transaction as the void.
/// </para>
/// <para>
/// <b>Money goes back the way it came.</b> The refunds are grouped by payment method rather than
/// written as one row, so cash taken at the desk is returned as cash and a card payment is
/// reversed on the card. That also means neither caller has to ask which method to use: voiding
/// from the screen and cancelling a check-in (which cannot ask anybody anything) behave the same.
/// </para>
/// </remarks>
public static class ServiceChargeRefunder
{
    /// <summary>
    /// Adds a refund per payment method that is still in credit for <paramref name="charge"/>, and
    /// returns their total. Nothing is added when nothing was paid, which is the ordinary case.
    /// The caller saves.
    /// </summary>
    public static async Task<decimal> RefundNetPaidAsync(
        IAppDbContext db, ServiceCharge charge, string reason, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(charge);

        var netPaidByMethod = await db.Payments
            .Where(payment => payment.ServiceChargeId == charge.Id)
            .GroupBy(payment => payment.Method)
            .Select(group => new
            {
                Method = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToListAsync(cancellationToken);

        var refunded = 0m;

        foreach (var row in netPaidByMethod.Where(row => row.NetPaid > 0))
        {
            var refund = Payment.RegisterRefundForServiceCharge(
                charge.Id, row.NetPaid, row.Method, referenceNumber: null, reason, userId, now);

            // The amount comes from rows this application wrote, each already within the money
            // rules, so a failure here is a bug rather than something a user can cause.
            if (refund.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Refunding a voided service charge produced an invalid payment: {refund.Error.Code}.");
            }

            db.Payments.Add(refund.Value);
            refunded += row.NetPaid;
        }

        return refunded;
    }
}

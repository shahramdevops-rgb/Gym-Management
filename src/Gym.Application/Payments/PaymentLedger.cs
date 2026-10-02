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

    public static Task<decimal> GetNetPaidForCafeOrderAsync(
        IAppDbContext db, Guid cafeOrderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.CafeOrderId == cafeOrderId)
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

    /// <summary>
    /// Net paid per payment method, only the methods still in credit: what voiding a charge gives
    /// back, one refund per row (BUSINESS_RULES.md §7).
    /// </summary>
    public static Task<IReadOnlyList<MethodNetPaid>> GetNetPaidByMethodForServiceChargeAsync(
        IAppDbContext db, Guid serviceChargeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return NetPaidByMethodAsync(
            db.Payments.Where(payment => payment.ServiceChargeId == serviceChargeId), cancellationToken);
    }

    /// <summary>The cafe twin: what cancelling an order gives back (BUSINESS_RULES.md §8).</summary>
    public static Task<IReadOnlyList<MethodNetPaid>> GetNetPaidByMethodForCafeOrderAsync(
        IAppDbContext db, Guid cafeOrderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return NetPaidByMethodAsync(
            db.Payments.Where(payment => payment.CafeOrderId == cafeOrderId), cancellationToken);
    }

    /// <summary>
    /// Net paid for several cafe orders in one query, keyed by order. An order with no payments at
    /// all is missing from the result, so callers read it with a default of zero.
    /// </summary>
    public static Task<Dictionary<Guid, decimal>> GetNetPaidForCafeOrdersAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> cafeOrderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.CafeOrderId != null && cafeOrderIds.Contains(payment.CafeOrderId.Value))
            .GroupBy(payment => payment.CafeOrderId!.Value)
            .Select(group => new
            {
                OrderId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.OrderId, row => row.NetPaid, cancellationToken);
    }

    /// <summary>
    /// Net paid for several service charges in one query, keyed by charge. A charge with no
    /// payments at all is missing from the result, so callers read it with a default of zero.
    /// </summary>
    public static Task<Dictionary<Guid, decimal>> GetNetPaidForServiceChargesAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> serviceChargeIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Payments
            .Where(payment => payment.ServiceChargeId != null && serviceChargeIds.Contains(payment.ServiceChargeId.Value))
            .GroupBy(payment => payment.ServiceChargeId!.Value)
            .Select(group => new
            {
                ChargeId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.ChargeId, row => row.NetPaid, cancellationToken);
    }

    private static async Task<IReadOnlyList<MethodNetPaid>> NetPaidByMethodAsync(
        IQueryable<Payment> payments, CancellationToken cancellationToken)
    {
        var rows = await payments
            .GroupBy(payment => payment.Method)
            .Select(group => new
            {
                Method = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => row.NetPaid > 0)
            .Select(row => new MethodNetPaid(row.Method, row.NetPaid))
            .ToList();
    }
}

/// <summary>How much is still in credit for one payment method on one item.</summary>
public sealed record MethodNetPaid(PaymentMethod Method, decimal NetPaid);

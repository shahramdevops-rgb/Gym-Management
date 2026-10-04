using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Cafe;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances;

/// <summary>
/// What a guest's visit still owes: its cafe orders and its service charges, هوازی, فروشگاه and
/// آنالیز (BUSINESS_RULES.md §7 <i>Guest visit</i>). A guest has no account, so this is what
/// check-out and cancel refuse on and what «تسویه یکجا» pays. Shared so the three can never
/// disagree about "unpaid".
/// </summary>
/// <remarks>
/// Read inside the caller's transaction, after it has locked the visit: an order, a charge or a
/// payment for the visit takes the same lock, so nothing changes between this read and the
/// caller's save.
/// </remarks>
public static class GuestPurchases
{
    /// <summary>
    /// The visit's standing orders and charges that still owe money, each with what it owes, oldest
    /// first within its kind.
    /// </summary>
    public static async Task<Unpaid> UnpaidAsync(
        IAppDbContext db, Guid attendanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var orders = await UnpaidOrdersAsync(db, attendanceId, cancellationToken);
        var charges = await UnpaidChargesAsync(db, attendanceId, cancellationToken);

        return new Unpaid(orders, charges);
    }

    private static async Task<IReadOnlyList<UnpaidOrder>> UnpaidOrdersAsync(
        IAppDbContext db, Guid attendanceId, CancellationToken cancellationToken)
    {
        var orders = await db.CafeOrders.AsNoTracking()
            .Where(order => order.AttendanceId == attendanceId && order.CancelledAt == null)
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return [];
        }

        var netPaidByOrder = await PaymentLedger.GetNetPaidForCafeOrdersAsync(
            db, orders.Select(order => order.Id).ToList(), cancellationToken);

        return orders
            .Select(order => new UnpaidOrder(order, order.Outstanding(netPaidByOrder.GetValueOrDefault(order.Id))))
            .Where(unpaid => unpaid.Outstanding > 0)
            .ToList();
    }

    private static async Task<IReadOnlyList<UnpaidCharge>> UnpaidChargesAsync(
        IAppDbContext db, Guid attendanceId, CancellationToken cancellationToken)
    {
        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => charge.AttendanceId == attendanceId && charge.VoidedAt == null)
            .OrderBy(charge => charge.CreatedAt)
            .ThenBy(charge => charge.Id)
            .ToListAsync(cancellationToken);

        if (charges.Count == 0)
        {
            return [];
        }

        var netPaidByCharge = await PaymentLedger.GetNetPaidForServiceChargesAsync(
            db, charges.Select(charge => charge.Id).ToList(), cancellationToken);

        return charges
            .Select(charge => new UnpaidCharge(charge, charge.Amount - netPaidByCharge.GetValueOrDefault(charge.Id)))
            .Where(unpaid => unpaid.Outstanding > 0)
            .ToList();
    }

    /// <summary>Everything a guest's visit still owes, by table.</summary>
    public sealed record Unpaid(IReadOnlyList<UnpaidOrder> Orders, IReadOnlyList<UnpaidCharge> Charges)
    {
        public bool Any => Orders.Count > 0 || Charges.Count > 0;

        public decimal Total => Orders.Sum(order => order.Outstanding) + Charges.Sum(charge => charge.Outstanding);
    }

    /// <summary>A standing order of a guest's visit and what is still owed on it.</summary>
    public sealed record UnpaidOrder(CafeOrder Order, decimal Outstanding);

    /// <summary>A standing هوازی or sale of a guest's visit and what is still owed on it.</summary>
    public sealed record UnpaidCharge(ServiceCharge Charge, decimal Outstanding);
}

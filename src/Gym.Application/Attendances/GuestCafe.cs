using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Cafe;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances;

/// <summary>
/// What a guest's visit still owes the cafe (BUSINESS_RULES.md §7 <i>Guest visit</i>). A guest has
/// no account, so this is what check-out and cancel refuse on and what «تسویه یکجا» pays. Shared so
/// the three can never disagree about "unpaid".
/// </summary>
/// <remarks>
/// Read inside the caller's transaction, after it has locked the visit: an order or a payment for
/// the visit takes the same lock, so nothing changes between this read and the caller's save.
/// </remarks>
public static class GuestCafe
{
    /// <summary>The visit's standing orders that still owe money, each with what it owes, oldest first.</summary>
    public static async Task<IReadOnlyList<UnpaidOrder>> UnpaidOrdersAsync(
        IAppDbContext db, Guid attendanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

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

    /// <summary>A standing order of a guest's visit and what is still owed on it.</summary>
    public sealed record UnpaidOrder(CafeOrder Order, decimal Outstanding);
}

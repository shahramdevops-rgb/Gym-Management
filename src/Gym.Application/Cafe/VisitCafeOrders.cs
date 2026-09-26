using Gym.Application.Common;
using Gym.Application.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe;

/// <summary>
/// What each of a page of visits bought from the cafe, ready to hang off an attendance row — the
/// cafe's twin of <c>VisitServiceCharges</c> (BUSINESS_RULES.md §8).
/// </summary>
/// <remarks>
/// A fixed number of queries for the whole page rather than a set per row: the orders with their
/// lines, their payments, and the members' names. Cancelled orders are left out, the way a voided
/// charge is: the board shows what is still owed for this visit, not the history of the till.
/// </remarks>
public static class VisitCafeOrders
{
    /// <summary>
    /// The standing orders of <paramref name="attendanceIds"/>, keyed by visit, oldest first. A
    /// visit that bought nothing is simply absent, which is the ordinary case.
    /// </summary>
    public static async Task<Dictionary<Guid, List<CafeOrderResponse>>> ByAttendanceAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> attendanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(attendanceIds);

        if (attendanceIds.Count == 0)
        {
            return [];
        }

        var orders = await db.CafeOrders.AsNoTracking()
            .Where(order => order.AttendanceId != null
                && attendanceIds.Contains(order.AttendanceId.Value)
                && order.CancelledAt == null)
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Include(order => order.Items)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return [];
        }

        var netPaidByOrder = await PaymentLedger.GetNetPaidForCafeOrdersAsync(
            db, orders.Select(order => order.Id).ToList(), cancellationToken);

        // Every order with a visit names its member (the domain and a check constraint both say so).
        var memberIds = orders.Select(order => order.MemberId!.Value).Distinct().ToList();
        var memberNames = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        return orders
            .GroupBy(order => order.AttendanceId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(order => CafeOrderResponse.From(
                        order,
                        memberNames.GetValueOrDefault(order.MemberId!.Value),
                        netPaidByOrder.GetValueOrDefault(order.Id)))
                    .ToList());
    }
}

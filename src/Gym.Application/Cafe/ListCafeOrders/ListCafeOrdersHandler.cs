using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.ListCafeOrders;

/// <summary>
/// The cafe's order history, newest first (task 7.3), for the whole counter or for one member.
/// Cancelled orders are included and marked by <see cref="CafeOrderResponse.CancelledAt"/>: this
/// is the operational view of what happened at the till, the same choice the attendance history
/// makes, not a revenue figure. The reports in Phase 9 decide for themselves what to leave out.
/// </summary>
public sealed class ListCafeOrdersHandler(IAppDbContext db)
{
    public async Task<PagedResponse<CafeOrderResponse>> Handle(
        ListCafeOrdersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var orders = db.CafeOrders.AsNoTracking();

        if (query.MemberId is { } memberId)
        {
            orders = orders.Where(order => order.MemberId == memberId);
        }

        if (query.AttendanceId is { } attendanceId)
        {
            orders = orders.Where(order => order.AttendanceId == attendanceId);
        }

        // A guest's orders left unpaid when the nightly job closed their visit, still to be paid or
        // cancelled with a reason (BUSINESS_RULES.md §7 Guest visit). "Unpaid" is payments less
        // refunds below the total, the same sum PaymentLedger makes, written here so the page and
        // its count stay one query each.
        if (query.UnpaidGuest == true)
        {
            orders = orders.Where(order => order.MemberId == null
                && order.AttendanceId != null
                && order.CancelledAt == null
                && db.Payments
                    .Where(payment => payment.CafeOrderId == order.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount) < order.TotalAmount);
        }

        // By the business date the order carries, not by the moment it was created: "the orders of
        // 3 Mehr" means the gym's day, and OrderedOn already is that day (BUSINESS_RULES.md §12).
        if (query.From is { } from)
        {
            orders = orders.Where(order => order.OrderedOn >= from);
        }

        if (query.To is { } to)
        {
            orders = orders.Where(order => order.OrderedOn <= to);
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        // Id breaks ties so paging never repeats or skips a row.
        var page = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(order => order.Items)
            .ToListAsync(cancellationToken);

        // Two batched lookups for the whole page, not one per row: whose order it was, and what
        // has been paid on it.
        var orderIds = page.Select(order => order.Id).ToList();
        var memberIds = page.Where(order => order.MemberId is not null).Select(order => order.MemberId!.Value)
            .Distinct().ToList();

        var memberNames = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        var netPaidByOrder = await PaymentLedger.GetNetPaidForCafeOrdersAsync(db, orderIds, cancellationToken);
        var guestNames = await CafeOrderGuests.NamesByVisitAsync(db, page, cancellationToken);

        var items = page
            .Select(order => CafeOrderResponse.From(
                order,
                order.MemberId is { } id ? memberNames.GetValueOrDefault(id) : null,
                netPaidByOrder.GetValueOrDefault(order.Id),
                CafeOrderGuests.For(guestNames, order)))
            .ToList();

        return new PagedResponse<CafeOrderResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

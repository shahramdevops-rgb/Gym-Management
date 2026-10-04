using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListSales;

/// <summary>
/// Everything the gym sold, newest first, with what has been paid on it (BUSINESS_RULES.md §12
/// <i>Sales in the history</i>, roadmap 6.5.30). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// Which sales match is <see cref="SaleRows"/>' job, shared with the totals. The union carries only
/// what it is sorted and filtered by (<see cref="SaleRow"/>). What a row says on screen is read
/// afterwards for the 20 rows of the page, one batched query per table.
/// </remarks>
public sealed class ListSalesHandler(IAppDbContext db, SaleRows saleRows, IUserNames users)
{
    public async Task<PagedResponse<HistorySaleResponse>> Handle(
        ListSalesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sales = saleRows.Matching(query);

        var totalCount = await sales.CountAsync(cancellationToken);

        var page = await sales
            .OrderByDescending(sale => sale.SoldAt)
            .ThenByDescending(sale => sale.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var details = await DetailsAsync(page, cancellationToken);

        var items = page
            .Select(sale => details.Describe(sale))
            .ToList();

        return new PagedResponse<HistorySaleResponse>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>What the page's rows say on screen: one query per table, then the names.</summary>
    private async Task<PageDetails> DetailsAsync(List<SaleRow> page, CancellationToken cancellationToken)
    {
        var subscriptionIds = IdsOf(page, SaleSource.Subscription);
        var chargeIds = page.Where(sale => sale.Source != SaleSource.Subscription && sale.Source != SaleSource.CafeOrder)
            .Select(sale => sale.Id).ToList();
        var orderIds = IdsOf(page, SaleSource.CafeOrder);

        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(subscription => subscriptionIds.Contains(subscription.Id))
            .Select(subscription => new
            {
                subscription.Id,
                subscription.MemberId,
                Plan = new PlanSummary(subscription.DurationDays, subscription.TotalSessions, subscription.IsSingleSession),
                subscription.CancellationReason,
            })
            .ToDictionaryAsync(
                subscription => subscription.Id,
                subscription => new Detail(
                    subscription.MemberId, null, subscription.Plan, null, null, null, null, subscription.CancellationReason),
                cancellationToken);

        // A guest's charge, like a guest's cafe order below, has no member: the name is on the visit.
        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => chargeIds.Contains(charge.Id))
            .Select(charge => new
            {
                charge.Id,
                charge.MemberId,
                GuestName = charge.MemberId == null
                    ? db.Attendances
                        .Where(attendance => attendance.Id == charge.AttendanceId)
                        .Select(attendance => attendance.GuestName)
                        .FirstOrDefault()
                    : null,
                charge.Description,
                charge.Quantity,
                charge.RecordedByUserId,
                charge.VoidReason,
            })
            .ToDictionaryAsync(
                charge => charge.Id,
                charge => new Detail(
                    charge.MemberId, charge.GuestName, null, charge.Description, charge.Quantity, null, charge.RecordedByUserId, charge.VoidReason),
                cancellationToken);

        // A guest's cafe order names their visit and no member (§8): the name is on the visit.
        var orders = await db.CafeOrders.AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .Select(order => new
            {
                order.Id,
                order.MemberId,
                GuestName = db.Attendances
                    .Where(attendance => attendance.Id == order.AttendanceId)
                    .Select(attendance => attendance.GuestName)
                    .FirstOrDefault(),
                order.PlacedByUserId,
                order.CancelReason,
            })
            .ToListAsync(cancellationToken);

        var orderItems = await db.CafeOrderItems.AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new { item.OrderId, item.ProductName, item.Quantity })
            .ToListAsync(cancellationToken);
        var itemsByOrder = orderItems.ToLookup(item => item.OrderId, item => new HistorySaleCafeItem(item.ProductName, item.Quantity));

        var byId = new Dictionary<Guid, Detail>(subscriptions);
        foreach (var (id, detail) in charges)
        {
            byId[id] = detail;
        }

        foreach (var order in orders)
        {
            byId[order.Id] = new Detail(
                order.MemberId, order.GuestName, null, null, null, [.. itemsByOrder[order.Id]], order.PlacedByUserId, order.CancelReason);
        }

        var memberIds = byId.Values.Where(detail => detail.MemberId is not null)
            .Select(detail => detail.MemberId!.Value).Distinct().ToList();
        var memberNames = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        var userIds = byId.Values.Where(detail => detail.RecordedByUserId is not null)
            .Select(detail => detail.RecordedByUserId!.Value).Distinct().ToList();
        var userNames = await users.FullNamesAsync(userIds, cancellationToken);

        return new PageDetails(byId, memberNames, userNames);
    }

    private static List<Guid> IdsOf(List<SaleRow> page, SaleSource source) =>
        page.Where(sale => sale.Source == source).Select(sale => sale.Id).ToList();

    private sealed record Detail(
        Guid? MemberId,
        string? GuestName,
        PlanSummary? Plan,
        string? Description,
        int? Quantity,
        IReadOnlyList<HistorySaleCafeItem>? CafeItems,
        Guid? RecordedByUserId,
        string? UndoReason);

    private sealed record PageDetails(
        Dictionary<Guid, Detail> ById,
        Dictionary<Guid, string> MemberNames,
        IReadOnlyDictionary<Guid, string> UserNames)
    {
        public HistorySaleResponse Describe(SaleRow sale)
        {
            var detail = ById[sale.Id];

            return new HistorySaleResponse(
                sale.Source,
                sale.Id,
                detail.MemberId,
                detail.MemberId is { } memberId ? MemberNames.GetValueOrDefault(memberId) : null,
                detail.GuestName,
                detail.Plan,
                detail.Description,
                detail.Quantity,
                detail.CafeItems,
                sale.Amount,
                sale.SoldAt,
                detail.RecordedByUserId is { } userId ? UserNames.GetValueOrDefault(userId) : null,
                sale.UndoneAt,
                detail.UndoReason,
                sale.NetPaid,
                PaymentStatusCalculator.Calculate(sale.Amount, sale.NetPaid));
        }
    }
}

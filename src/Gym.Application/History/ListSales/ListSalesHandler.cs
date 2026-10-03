using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListSales;

/// <summary>
/// Everything the gym sold, newest first, with what has been paid on it (BUSINESS_RULES.md §12
/// <i>Sales in the history</i>, roadmap 6.5.30). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// <para>
/// Sales live in three tables (subscriptions, service charges, cafe orders), but the list is paged
/// and filtered by whether each is paid, so the three are put together in the database as one
/// <c>UNION ALL</c> and counted, filtered and paged there. Merging them in memory would mean loading
/// every sale of the range to show 20.
/// </para>
/// <para>
/// The union carries only what it is sorted and filtered by (<see cref="SaleRow"/>). What a row says
/// on screen is read afterwards for the 20 rows of the page, one batched query per table.
/// </para>
/// </remarks>
public sealed class ListSalesHandler(IAppDbContext db, IGymCalendar calendar, IUserNames users)
{
    public async Task<PagedResponse<HistorySaleResponse>> Handle(
        ListSalesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sales = Branches(query)
            .Aggregate((all, next) => all.Concat(next));

        // Net paid covers the amount: a free plan is paid (§4). Cancelled and voided match neither.
        sales = query.Paid switch
        {
            SalePaidFilter.Paid => sales.Where(sale => sale.UndoneAt == null && sale.NetPaid >= sale.Amount),
            SalePaidFilter.Unpaid => sales.Where(sale => sale.UndoneAt == null && sale.NetPaid < sale.Amount),
            _ => sales,
        };

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

    /// <summary>
    /// One query per kind of sale the request asks for, each already narrowed by date and member.
    /// The three charge kinds are three branches, so every branch names its source as a constant.
    /// </summary>
    private List<IQueryable<SaleRow>> Branches(ListSalesQuery query)
    {
        var branches = new List<IQueryable<SaleRow>>();

        if (query.Source is null or SaleSource.Subscription)
        {
            branches.Add(Subscriptions(query));
        }

        foreach (var (source, kind) in ChargeSources)
        {
            if (query.Source is null || query.Source == source)
            {
                branches.Add(Charges(query, source, kind));
            }
        }

        if (query.Source is null or SaleSource.CafeOrder)
        {
            branches.Add(CafeOrders(query));
        }

        return branches;
    }

    private static readonly (SaleSource Source, ServiceChargeKind Kind)[] ChargeSources =
    [
        (SaleSource.Cardio, ServiceChargeKind.Cardio),
        (SaleSource.Miscellaneous, ServiceChargeKind.Miscellaneous),
        (SaleSource.Analysis, ServiceChargeKind.Analysis),
    ];

    private IQueryable<SaleRow> Subscriptions(ListSalesQuery query)
    {
        var subscriptions = db.Subscriptions.AsNoTracking();

        // A plan belongs to the day it was sold, in the gym's time zone (§4 «تاریخ فروش», §12).
        if (query.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            subscriptions = subscriptions.Where(subscription => subscription.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            subscriptions = subscriptions.Where(subscription => subscription.CreatedAt < end);
        }

        if (query.MemberId is { } memberId)
        {
            subscriptions = subscriptions.Where(subscription => subscription.MemberId == memberId);
        }

        return subscriptions.Select(subscription => new SaleRow
        {
            Source = SaleSource.Subscription,
            Id = subscription.Id,
            Amount = subscription.Price,
            NetPaid = db.Payments
                .Where(payment => payment.SubscriptionId == subscription.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = subscription.CreatedAt,
            UndoneAt = subscription.CancelledAt,
        });
    }

    private IQueryable<SaleRow> Charges(ListSalesQuery query, SaleSource source, ServiceChargeKind kind)
    {
        var charges = db.ServiceCharges.AsNoTracking().Where(charge => charge.Kind == kind);

        // By the business date the charge carries: it already is the gym's day (§12).
        if (query.From is { } from)
        {
            charges = charges.Where(charge => charge.ChargedOn >= from);
        }

        if (query.To is { } to)
        {
            charges = charges.Where(charge => charge.ChargedOn <= to);
        }

        if (query.MemberId is { } memberId)
        {
            charges = charges.Where(charge => charge.MemberId == memberId);
        }

        return charges.Select(charge => new SaleRow
        {
            Source = source,
            Id = charge.Id,
            Amount = charge.Amount,
            NetPaid = db.Payments
                .Where(payment => payment.ServiceChargeId == charge.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = charge.CreatedAt,
            UndoneAt = charge.VoidedAt,
        });
    }

    private IQueryable<SaleRow> CafeOrders(ListSalesQuery query)
    {
        var orders = db.CafeOrders.AsNoTracking();

        if (query.From is { } from)
        {
            orders = orders.Where(order => order.OrderedOn >= from);
        }

        if (query.To is { } to)
        {
            orders = orders.Where(order => order.OrderedOn <= to);
        }

        if (query.MemberId is { } memberId)
        {
            orders = orders.Where(order => order.MemberId == memberId);
        }

        return orders.Select(order => new SaleRow
        {
            Source = SaleSource.CafeOrder,
            Id = order.Id,
            Amount = order.TotalAmount,
            NetPaid = db.Payments
                .Where(payment => payment.CafeOrderId == order.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = order.CreatedAt,
            UndoneAt = order.CancelledAt,
        });
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

        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => chargeIds.Contains(charge.Id))
            .ToDictionaryAsync(
                charge => charge.Id,
                charge => new Detail(
                    charge.MemberId, null, null, charge.Description, charge.Quantity, null, charge.RecordedByUserId, charge.VoidReason),
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

    /// <summary>
    /// What the union carries: enough to filter by paid, sort and page. A class with settable members,
    /// not a record, because EF Core matches the branches of a set operation member by member.
    /// </summary>
    private sealed class SaleRow
    {
        public SaleSource Source { get; init; }

        public Guid Id { get; init; }

        public decimal Amount { get; init; }

        public decimal NetPaid { get; init; }

        public DateTimeOffset SoldAt { get; init; }

        public DateTimeOffset? UndoneAt { get; init; }
    }

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

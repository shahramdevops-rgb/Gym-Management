using Gym.Application.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListSales;

/// <summary>
/// Every sale a filter lets through, as one query the database runs: the sales list pages it
/// (<see cref="ListSalesHandler"/>), the totals add it up (<c>SalesTotalsHandler</c>, roadmap
/// 6.5.32). One place decides which sales count, so the two can never disagree.
/// </summary>
/// <remarks>
/// Sales live in three tables (subscriptions, service charges, cafe orders), but the list is paged
/// and filtered by whether each is paid, so the three are put together in the database as one
/// <c>UNION ALL</c> and counted, filtered, paged and summed there. Merging them in memory would mean
/// loading every sale of the range to show 20.
/// </remarks>
public sealed class SaleRows(IAppDbContext db, IGymCalendar calendar)
{
    /// <summary>The sales <paramref name="filter"/> asks for, not yet sorted.</summary>
    public IQueryable<SaleRow> Matching(ISalesFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var sales = Branches(filter)
            .Aggregate((all, next) => all.Concat(next));

        // Net paid covers the amount: a free plan is paid (§4). Cancelled and voided match neither.
        return filter.Paid switch
        {
            SalePaidFilter.Paid => sales.Where(sale => sale.UndoneAt == null && sale.NetPaid >= sale.Amount),
            SalePaidFilter.Unpaid => sales.Where(sale => sale.UndoneAt == null && sale.NetPaid < sale.Amount),
            _ => sales,
        };
    }

    /// <summary>
    /// One query per kind of sale the request asks for, each already narrowed by date and member.
    /// The three charge kinds are three branches, so every branch names its source as a constant.
    /// </summary>
    private List<IQueryable<SaleRow>> Branches(ISalesFilter filter)
    {
        var branches = new List<IQueryable<SaleRow>>();

        if (filter.Source is null or SaleSource.Subscription)
        {
            branches.Add(Subscriptions(filter));
        }

        foreach (var (source, kind) in ChargeSources)
        {
            if (filter.Source is null || filter.Source == source)
            {
                branches.Add(Charges(filter, source, kind));
            }
        }

        if (filter.Source is null or SaleSource.CafeOrder)
        {
            branches.Add(CafeOrders(filter));
        }

        return branches;
    }

    private static readonly (SaleSource Source, ServiceChargeKind Kind)[] ChargeSources =
    [
        (SaleSource.Cardio, ServiceChargeKind.Cardio),
        (SaleSource.Miscellaneous, ServiceChargeKind.Miscellaneous),
        (SaleSource.Analysis, ServiceChargeKind.Analysis),
        (SaleSource.Other, ServiceChargeKind.Other),
    ];

    private IQueryable<SaleRow> Subscriptions(ISalesFilter filter)
    {
        var subscriptions = db.Subscriptions.AsNoTracking();

        // A plan belongs to the day it was sold, in the gym's time zone (§4 «تاریخ فروش», §12).
        if (filter.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            subscriptions = subscriptions.Where(subscription => subscription.CreatedAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            subscriptions = subscriptions.Where(subscription => subscription.CreatedAt < end);
        }

        if (filter.MemberId is { } memberId)
        {
            subscriptions = subscriptions.Where(subscription => subscription.MemberId == memberId);
        }

        return subscriptions.Select(subscription => new SaleRow
        {
            Source = SaleSource.Subscription,
            Id = subscription.Id,
            MemberId = subscription.MemberId,
            Amount = subscription.Price,
            NetPaid = db.Payments
                .Where(payment => payment.SubscriptionId == subscription.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = subscription.CreatedAt,
            UndoneAt = subscription.CancelledAt,
        });
    }

    private IQueryable<SaleRow> Charges(ISalesFilter filter, SaleSource source, ServiceChargeKind kind)
    {
        var charges = db.ServiceCharges.AsNoTracking().Where(charge => charge.Kind == kind);

        // By the business date the charge carries: it already is the gym's day (§12).
        if (filter.From is { } from)
        {
            charges = charges.Where(charge => charge.ChargedOn >= from);
        }

        if (filter.To is { } to)
        {
            charges = charges.Where(charge => charge.ChargedOn <= to);
        }

        if (filter.MemberId is { } memberId)
        {
            charges = charges.Where(charge => charge.MemberId == memberId);
        }

        return charges.Select(charge => new SaleRow
        {
            Source = source,
            Id = charge.Id,
            MemberId = charge.MemberId,
            Amount = charge.Amount,
            NetPaid = db.Payments
                .Where(payment => payment.ServiceChargeId == charge.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = charge.CreatedAt,
            UndoneAt = charge.VoidedAt,
        });
    }

    private IQueryable<SaleRow> CafeOrders(ISalesFilter filter)
    {
        var orders = db.CafeOrders.AsNoTracking();

        if (filter.From is { } from)
        {
            orders = orders.Where(order => order.OrderedOn >= from);
        }

        if (filter.To is { } to)
        {
            orders = orders.Where(order => order.OrderedOn <= to);
        }

        if (filter.MemberId is { } memberId)
        {
            orders = orders.Where(order => order.MemberId == memberId);
        }

        return orders.Select(order => new SaleRow
        {
            Source = SaleSource.CafeOrder,
            Id = order.Id,
            MemberId = order.MemberId,
            Amount = order.TotalAmount,
            NetPaid = db.Payments
                .Where(payment => payment.CafeOrderId == order.Id)
                .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            SoldAt = order.CreatedAt,
            UndoneAt = order.CancelledAt,
        });
    }
}

/// <summary>
/// What the union carries: enough to filter by paid, sort, page and sum. A class with settable
/// members, not a record, because EF Core matches the branches of a set operation member by member.
/// </summary>
public sealed class SaleRow
{
    public SaleSource Source { get; init; }

    public Guid Id { get; init; }

    /// <summary>Who bought it; <c>null</c> for a walk-in at the cafe and for a guest.</summary>
    public Guid? MemberId { get; init; }

    public decimal Amount { get; init; }

    public decimal NetPaid { get; init; }

    public DateTimeOffset SoldAt { get; init; }

    public DateTimeOffset? UndoneAt { get; init; }
}

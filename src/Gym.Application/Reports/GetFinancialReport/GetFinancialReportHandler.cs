using System.Diagnostics;

using Gym.Application.Common;
using Gym.Application.History.ListSales;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetFinancialReport;

/// <summary>
/// Revenue, sales, expenses and profit for a range and for the range before it
/// (BUSINESS_RULES.md §12 <i>Financial report</i>, roadmap 9.1). Owner only; the endpoint's policy
/// says so.
/// </summary>
/// <remarks>
/// <para>
/// Each range is read with one query for its payments and one for its expenses. Every breakdown
/// (by source, method, staff member, category, day) is then added up here from those same rows, so
/// the parts always add up to the totals: no two queries can disagree about which rows counted.
/// </para>
/// <para>
/// The payments come back as small rows rather than as sums because a day is a day in the gym's time
/// zone, and turning a moment into that day is done here with <see cref="TimeZoneInfo"/>, as the
/// chart under the map does (<c>TodayByHourHandler</c>), not with SQL that names the zone. A year of
/// one gym's payments is a few tens of thousands of rows of six columns, which is little to send.
/// </para>
/// </remarks>
public sealed class GetFinancialReportHandler(
    IAppDbContext db, IGymCalendar calendar, SaleRows saleRows, IUserNames users)
{
    /// <summary>The order the desk sees the methods in (§5 <i>Confirming money at the desk</i>).</summary>
    private static readonly PaymentMethod[] MethodOrder =
        [PaymentMethod.Card, PaymentMethod.BankTransfer, PaymentMethod.Cash];

    public async Task<FinancialReportResponse> Handle(GetFinancialReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The validator has made sure both are there, in order, and at most a year apart.
        var from = query.From!.Value;
        var to = query.To!.Value;
        var length = to.DayNumber - from.DayNumber + 1;
        var previousFrom = from.AddDays(-length);
        var previousTo = from.AddDays(-1);

        var current = await LoadAsync(from, to, cancellationToken);
        var previous = await LoadAsync(previousFrom, previousTo, cancellationToken);

        var staffNames = await users.FullNamesAsync(
            current.Payments.Concat(previous.Payments).Select(payment => payment.ReceivedByUserId).Distinct().ToList(),
            cancellationToken);
        var categoryNames = await CategoryNamesAsync(
            current.Expenses.Concat(previous.Expenses).Select(expense => expense.CategoryId).Distinct().ToList(),
            cancellationToken);

        return new FinancialReportResponse(
            Period(from, to, current, staffNames, categoryNames),
            Period(previousFrom, previousTo, previous, staffNames, categoryNames),
            Days(from, length, current));
    }

    private async Task<RangeData> LoadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(to.AddDays(1));

        // Every payment and refund of the range, those on a cancelled or voided item included: the
        // money did move (§12). What it was for is read with the payment, as the history does.
        var payments = await db.Payments
            .AsNoTracking()
            .Where(payment => payment.PaidAt >= start && payment.PaidAt < end)
            .Select(payment => new
            {
                payment.PaidAt,
                payment.Kind,
                payment.Amount,
                payment.Method,
                payment.ReceivedByUserId,
                IsSingleSession = db.Subscriptions
                    .Where(subscription => subscription.Id == payment.SubscriptionId)
                    .Select(subscription => (bool?)subscription.IsSingleSession)
                    .FirstOrDefault(),
                ServiceKind = db.ServiceCharges
                    .Where(charge => charge.Id == payment.ServiceChargeId)
                    .Select(charge => (ServiceChargeKind?)charge.Kind)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // Summed per day and category by the database: the breakdowns need nothing finer.
        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(expense => expense.VoidedAt == null && expense.ExpenseDate >= from && expense.ExpenseDate <= to)
            .GroupBy(expense => new { expense.ExpenseDate, expense.CategoryId })
            .Select(group => new ExpenseSum(group.Key.ExpenseDate, group.Key.CategoryId, group.Sum(expense => expense.Amount)))
            .ToListAsync(cancellationToken);

        var liveSales = saleRows.Matching(new SalesInRange(from, to))
            .Where(sale => sale.UndoneAt == null);

        // «فروش» on the dashboard leaves فروشگاه and آنالیز out (decided with the developer,
        // 1405/07/14); they are still counted below, in each source's «sold».
        var gymSales = liveSales
            .Where(sale => sale.Source != SaleSource.Miscellaneous && sale.Source != SaleSource.Analysis);
        var sales = await gymSales.SumAsync(sale => sale.Amount, cancellationToken);

        // How much of those same sales has been paid so far, refunds taken off, whenever it was
        // paid; the rest of «فروش» is still owed («نسیه»). Decided with the developer, 1405/07/14,
        // so that «فروش» is never read as money that should be in the drawer.
        var salesPaid = await gymSales.SumAsync(sale => sale.NetPaid, cancellationToken);

        // The same sales, counted, priced and with what has been paid on them per kind (§12
        // *Financial report*: «تعداد فروش», and the figures of «خرید پلن» and «تک‌جلسه‌ای» with
        // their «پرداخت‌شده» and «نسیه», by the day each was sold, 1405/07/14).
        var soldByKind = await liveSales
            .GroupBy(sale => sale.Source)
            .Select(group => new
            {
                Source = group.Key,
                Count = group.Count(),
                Amount = group.Sum(sale => sale.Amount),
                Paid = group.Sum(sale => sale.NetPaid),
            })
            .ToDictionaryAsync(row => row.Source, row => new Sold(row.Count, row.Amount, row.Paid), cancellationToken);

        // The union knows a plan, not a single visit, so the single visits among those plans are
        // counted on their own, with the union's own rule: sold in the range, not cancelled.
        var singleVisits = db.Subscriptions
            .AsNoTracking()
            .Where(subscription => subscription.IsSingleSession &&
                subscription.CancelledAt == null &&
                subscription.CreatedAt >= start &&
                subscription.CreatedAt < end);
        var singleVisitsSold = new Sold(
            await singleVisits.CountAsync(cancellationToken),
            await singleVisits.SumAsync(subscription => subscription.Price, cancellationToken),
            await singleVisits
                .Select(subscription => db.Payments
                    .Where(payment => payment.SubscriptionId == subscription.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount))
                .SumAsync(cancellationToken));

        var plansSold = soldByKind.GetValueOrDefault(SaleSource.Subscription) ?? Sold.None;
        var sold = new Dictionary<RevenueSource, Sold>
        {
            [RevenueSource.Membership] = new(
                plansSold.Count - singleVisitsSold.Count,
                plansSold.Amount - singleVisitsSold.Amount,
                plansSold.Paid - singleVisitsSold.Paid),
            [RevenueSource.SingleSession] = singleVisitsSold,
            [RevenueSource.Cardio] = soldByKind.GetValueOrDefault(SaleSource.Cardio) ?? Sold.None,
            [RevenueSource.Miscellaneous] = soldByKind.GetValueOrDefault(SaleSource.Miscellaneous) ?? Sold.None,
            [RevenueSource.Analysis] = soldByKind.GetValueOrDefault(SaleSource.Analysis) ?? Sold.None,
            [RevenueSource.Cafe] = soldByKind.GetValueOrDefault(SaleSource.CafeOrder) ?? Sold.None,
        };

        return new RangeData(
            payments
                .Select(payment => new PaymentFact(
                    calendar.DayOf(payment.PaidAt),
                    payment.Kind == PaymentKind.Payment ? payment.Amount : 0m,
                    payment.Kind == PaymentKind.Refund ? payment.Amount : 0m,
                    payment.Method,
                    payment.ReceivedByUserId,
                    SourceOf(payment.IsSingleSession, payment.ServiceKind)))
                .ToList(),
            expenses,
            sales,
            salesPaid,
            sold);
    }

    private static FinancialPeriodResponse Period(
        DateOnly from,
        DateOnly to,
        RangeData data,
        IReadOnlyDictionary<Guid, string> staffNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        var revenue = Flow(data.Payments);

        var bySource = Enum.GetValues<RevenueSource>()
            .Select(source =>
            {
                var sold = data.Sold.GetValueOrDefault(source) ?? Sold.None;
                return new RevenueBySourceResponse(
                    source,
                    Flow(data.Payments.Where(payment => payment.Source == source)),
                    sold.Count,
                    sold.Amount,
                    sold.Paid,
                    sold.Amount - sold.Paid);
            })
            .ToList();

        var byMethod = ByMethod(data.Payments);

        // «دریافتی», what should be in the drawer, the card reader and the account, and apart from
        // it the money of فروشگاه and آنالیز, which belongs to someone else (decided with the
        // developer, 1405/07/14).
        var receivedByMethod = ByMethod(data.Payments.Where(payment => !IsShopOrAnalysis(payment.Source)));
        var shopAndAnalysisByMethod = ByMethod(data.Payments.Where(payment => IsShopOrAnalysis(payment.Source)));

        var byStaff = data.Payments
            .GroupBy(payment => payment.ReceivedByUserId)
            .Select(group => new RevenueByStaffResponse(
                group.Key, staffNames.GetValueOrDefault(group.Key) ?? string.Empty, Flow(group)))
            .OrderByDescending(row => row.Money.Net)
            .ThenBy(row => row.FullName, StringComparer.Ordinal)
            .ToList();

        var byCategory = data.Expenses
            .GroupBy(expense => expense.CategoryId)
            .Select(group => new ExpensesByCategoryResponse(
                group.Key,
                categoryNames.GetValueOrDefault(group.Key) ?? string.Empty,
                group.Sum(expense => expense.Amount)))
            .OrderByDescending(row => row.Amount)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();

        var expenses = byCategory.Sum(row => row.Amount);

        // «سود خالص» is the gym's own revenue minus every expense: فروشگاه and آنالیز are left
        // out, as they are out of «فروش» (decided with the developer, 1405/07/14).
        var ownRevenue = bySource
            .Where(row => !IsShopOrAnalysis(row.Source))
            .Sum(row => row.Money.Net);

        return new FinancialPeriodResponse(
            from,
            to,
            revenue,
            bySource,
            byMethod,
            receivedByMethod,
            shopAndAnalysisByMethod,
            byStaff,
            data.Sales,
            data.SalesPaid,
            data.Sales - data.SalesPaid,
            expenses,
            byCategory,
            ownRevenue - expenses);
    }

    /// <summary>The three methods in the desk's order, each with what came in and went back out.</summary>
    private static List<RevenueByMethodResponse> ByMethod(IEnumerable<PaymentFact> payments)
    {
        var list = payments.ToList();

        return MethodOrder
            .Select(method => new RevenueByMethodResponse(method, Flow(list.Where(payment => payment.Method == method))))
            .ToList();
    }

    /// <summary>فروشگاه and آنالیز: someone else's business, run at the gym's desk (1405/07/14).</summary>
    private static bool IsShopOrAnalysis(RevenueSource source) =>
        source is RevenueSource.Miscellaneous or RevenueSource.Analysis;

    private static List<FinancialDayResponse> Days(DateOnly from, int length, RangeData data)
    {
        var revenueByDay = data.Payments
            .GroupBy(payment => payment.Day)
            .ToDictionary(group => group.Key, group => Flow(group).Net);
        var expensesByDay = data.Expenses
            .GroupBy(expense => expense.Date)
            .ToDictionary(group => group.Key, group => group.Sum(expense => expense.Amount));

        return Enumerable.Range(0, length)
            .Select(offset => from.AddDays(offset))
            .Select(day => new FinancialDayResponse(
                day, revenueByDay.GetValueOrDefault(day), expensesByDay.GetValueOrDefault(day)))
            .ToList();
    }

    private static MoneyFlowResponse Flow(IEnumerable<PaymentFact> payments)
    {
        decimal received = 0m, refunded = 0m;
        foreach (var payment in payments)
        {
            received += payment.Received;
            refunded += payment.Refunded;
        }

        return new MoneyFlowResponse(received, refunded, received - refunded);
    }

    /// <summary>
    /// A payment points at exactly one of a subscription, a service charge or a cafe order (§5, a
    /// check constraint): a single-session flag means the first, a kind the second, neither the third.
    /// </summary>
    private static RevenueSource SourceOf(bool? isSingleSession, ServiceChargeKind? serviceKind) =>
        (isSingleSession, serviceKind) switch
        {
            (true, _) => RevenueSource.SingleSession,
            (false, _) => RevenueSource.Membership,
            (null, ServiceChargeKind.Cardio) => RevenueSource.Cardio,
            (null, ServiceChargeKind.Miscellaneous) => RevenueSource.Miscellaneous,
            (null, ServiceChargeKind.Analysis) => RevenueSource.Analysis,
            (null, null) => RevenueSource.Cafe,
            _ => throw new UnreachableException($"Service charge kind {serviceKind} has no revenue source."),
        };

    private async Task<IReadOnlyDictionary<Guid, string>> CategoryNamesAsync(
        List<Guid> categoryIds, CancellationToken cancellationToken) =>
            await db.ExpenseCategories
                .AsNoTracking()
                .Where(category => categoryIds.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

    /// <summary>One payment or refund, as the breakdowns need it.</summary>
    private sealed record PaymentFact(
        DateOnly Day,
        decimal Received,
        decimal Refunded,
        PaymentMethod Method,
        Guid ReceivedByUserId,
        RevenueSource Source);

    private sealed record ExpenseSum(DateOnly Date, Guid CategoryId, decimal Amount);

    private sealed record RangeData(
        List<PaymentFact> Payments,
        List<ExpenseSum> Expenses,
        decimal Sales,
        decimal SalesPaid,
        IReadOnlyDictionary<RevenueSource, Sold> Sold);

    /// <summary>How many of one kind were sold in a range, and their price.</summary>
    private sealed record Sold(int Count, decimal Amount, decimal Paid)
    {
        public static readonly Sold None = new(0, 0m, 0m);
    }

    /// <summary>Every sale of a range, whoever bought it and whatever it was (§12 <i>Sales in the history</i>).</summary>
    private sealed record SalesInRange(
        DateOnly? From,
        DateOnly? To,
        Guid? MemberId = null,
        SaleSource? Source = null,
        SalePaidFilter? Paid = null) : ISalesFilter;
}

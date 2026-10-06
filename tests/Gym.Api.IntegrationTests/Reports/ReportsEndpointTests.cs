using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.Expenses;
using Gym.Application.History.SalesTotals;
using Gym.Application.Reports;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Reports.GetReceivables;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Expenses;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Reports;

/// <summary>
/// The Owner's figures: <c>GET /api/reports/financial</c> and <c>GET /api/reports/receivables</c>
/// (BUSINESS_RULES.md §12 <i>Financial report</i>, <i>Receivables</i>, roadmap 9.1).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ReportsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly Guid RentId = new("0b3f6685-b1a2-4979-acd9-c1fe848f156b");

    private static int _phoneSuffix;

    // ---- Financial report: revenue ----

    [Fact]
    public async Task Financial_EverySource_SplitsRevenueBySourceAndMethod()
    {
        var (owner, token, _) = await OwnerClientAsync();

        // Membership: paid by card, partly refunded by card.
        var membership = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{membership.Id}/payments", 300_000m, "Card");
        await PostOkAsync(owner, token, $"/api/subscriptions/{membership.Id}/refunds",
            new { amount = 100_000m, method = "Card", reason = "اشتباه در ثبت" });

        // A single visit, apart from membership.
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var single = await SellSingleVisitOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{single.Id}/payments", 150_000m, "Cash");

        // هوازی, فروشگاه and آنالیز on one visit, and a walk-in at the cafe.
        var visitor = await AddMemberAsync("رضا کریمی");
        await AssignOkAsync(owner, token, visitor.Id);
        var visit = await TestLockers.CheckInOkAsync(owner, token, visitor.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "Cash");
        var shop = await RecordShopOkAsync(owner, token, visit.Id, "دستکش", 200_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{shop.Id}/payments", 200_000m, "BankTransfer");
        var analysis = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 80_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{analysis.Id}/payments", 80_000m, "Card");
        await WalkInOrderAsync(owner, token, 60_000m);

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        current.Revenue.ShouldBe(new MoneyFlowResponse(840_000m, 100_000m, 740_000m));
        current.BySource.Select(row => row.Source).ShouldBe(Enum.GetValues<RevenueSource>());
        Money(current, RevenueSource.Membership).ShouldBe(new MoneyFlowResponse(300_000m, 100_000m, 200_000m));
        Money(current, RevenueSource.SingleSession).ShouldBe(new MoneyFlowResponse(150_000m, 0m, 150_000m));
        Money(current, RevenueSource.Cardio).ShouldBe(new MoneyFlowResponse(50_000m, 0m, 50_000m));
        Money(current, RevenueSource.Miscellaneous).ShouldBe(new MoneyFlowResponse(200_000m, 0m, 200_000m));
        Money(current, RevenueSource.Analysis).ShouldBe(new MoneyFlowResponse(80_000m, 0m, 80_000m));
        Money(current, RevenueSource.Cafe).ShouldBe(new MoneyFlowResponse(60_000m, 0m, 60_000m));

        // In the desk's order: card, bank transfer, cash (§5).
        current.ByMethod.Select(row => row.Method)
            .ShouldBe([PaymentMethod.Card, PaymentMethod.BankTransfer, PaymentMethod.Cash]);
        current.ByMethod[0].Money.ShouldBe(new MoneyFlowResponse(380_000m, 100_000m, 280_000m));
        current.ByMethod[1].Money.ShouldBe(new MoneyFlowResponse(200_000m, 0m, 200_000m));
        current.ByMethod[2].Money.ShouldBe(new MoneyFlowResponse(260_000m, 0m, 260_000m));
    }

    [Fact]
    public async Task Financial_SalesOfTheRange_AreBesideRevenueAndLeaveCancelledOut()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var owed = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{owed.Id}/payments", 100_000m, "Cash");
        var cancelled = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 800_000m);
        await PostOkAsync(owner, token, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "انصراف" });

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        current.Sales.ShouldBe(900_000m);
        current.Revenue.Net.ShouldBe(100_000m);
    }

    [Fact]
    public async Task Financial_Sales_LeaveTheShopAndAnalysisOut()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var visitor = await AddMemberAsync("رضا کریمی");
        await AssignOkAsync(owner, token, visitor.Id);
        var visit = await TestLockers.CheckInOkAsync(owner, token, visitor.Id);
        await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await RecordShopOkAsync(owner, token, visit.Id, "دستکش", 200_000m);
        await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 80_000m);
        await WalkInOrderAsync(owner, token, 60_000m);

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        // §12: the plan, هوازی and the cafe; not فروشگاه or آنالیز (1405/07/14).
        current.Sales.ShouldBe(900_000m + 50_000m + 60_000m);
        // They are still sales of their own source.
        Sold(current, RevenueSource.Miscellaneous).ShouldBe(1);
        Sold(current, RevenueSource.Analysis).ShouldBe(1);
    }

    [Fact]
    public async Task Financial_SalesPaidOwedAndReceived_SplitWhatWasSoldFromWhatCameIn()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var visitor = await AddMemberAsync("رضا کریمی");
        var plan = await AssignOkAsync(owner, token, visitor.Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 500_000m, "Card");
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 100_000m, "Cash");
        await PostOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/refunds",
            new { amount = 40_000m, method = "Cash", reason = "اشتباه در ثبت" });
        var visit = await TestLockers.CheckInOkAsync(owner, token, visitor.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "BankTransfer");
        await WalkInOrderAsync(owner, token, 60_000m);
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        await SellSingleVisitOkAsync(owner, token, (await AddMemberAsync("نرگس موسوی")).Id);

        // A shop sale is someone else's money; a plan sold yesterday and paid today is today's
        // money but not today's sale.
        var shop = await RecordShopOkAsync(owner, token, visit.Id, "دستکش", 200_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{shop.Id}/payments", 200_000m, "Card");
        var older = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id);
        await MoveSubscriptionSaleAsync(older.Id, Calendar().StartOfDayUtc(Today().AddDays(-1)).AddHours(12));
        await PayOkAsync(owner, token, $"/api/subscriptions/{older.Id}/payments", 900_000m, "Card");

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        // §12 (1405/07/14): «فروش», of which paid and still owed («نسیه»).
        current.Sales.ShouldBe(900_000m + 50_000m + 60_000m + 150_000m);
        current.SalesPaid.ShouldBe(560_000m + 50_000m + 60_000m);
        current.SalesOwed.ShouldBe(340_000m + 150_000m);

        // «دریافتی»: everything that came in today, the older plan's payment included, the shop out.
        current.ReceivedByMethod.Select(row => (row.Method, row.Money.Net)).ShouldBe(
        [
            (PaymentMethod.Card, 500_000m + 900_000m),
            (PaymentMethod.BankTransfer, 50_000m),
            (PaymentMethod.Cash, 100_000m - 40_000m + 60_000m),
        ]);
        current.ShopAndAnalysisByMethod.Select(row => (row.Method, row.Money.Net)).ShouldBe(
        [
            (PaymentMethod.Card, 200_000m),
            (PaymentMethod.BankTransfer, 0m),
            (PaymentMethod.Cash, 0m),
        ]);

        // «خرید پلن» and «تک‌جلسه‌ای» by the day they were sold, paid or not, and of it what was
        // paid and what is still owed: the older plan paid today is not this range's sale.
        var plans = current.BySource.Single(row => row.Source == RevenueSource.Membership);
        (plans.SoldAmount, plans.SoldPaid, plans.SoldOwed).ShouldBe((900_000m, 560_000m, 340_000m));
        var singleVisits = current.BySource.Single(row => row.Source == RevenueSource.SingleSession);
        (singleVisits.SoldAmount, singleVisits.SoldPaid, singleVisits.SoldOwed).ShouldBe((150_000m, 0m, 150_000m));
    }

    [Fact]
    public async Task Financial_Sold_CountsEachSourcesSalesOfTheRangeByTheirOwnDay()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var today = Today();

        // Two plans sold today, one of them still unpaid: a sale counts whether or not it is paid.
        await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id);

        // A cancelled plan is not a sale (§12, like «فروش»).
        var cancelled = await AssignOkAsync(owner, token, (await AddMemberAsync("سارا احمدی")).Id);
        await PostOkAsync(owner, token, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "انصراف" });

        // Sold before the range and paid in it: money in the range, not a sale of it.
        var older = await AssignOkAsync(owner, token, (await AddMemberAsync("رضا کریمی")).Id);
        await MoveSubscriptionSaleAsync(older.Id, Calendar().StartOfDayUtc(today.AddDays(-1)).AddHours(12));
        await PayOkAsync(owner, token, $"/api/subscriptions/{older.Id}/payments", 900_000m, "Cash");

        // Single visits are counted apart from plans.
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        await SellSingleVisitOkAsync(owner, token, (await AddMemberAsync("نرگس موسوی")).Id);
        await SellSingleVisitOkAsync(owner, token, (await AddMemberAsync("حسین نوری")).Id);
        await SellSingleVisitOkAsync(owner, token, (await AddMemberAsync("زهرا صادقی")).Id);

        await WalkInOrderAsync(owner, token, 60_000m);

        var report = await FinancialOkAsync(owner, token, TodayRange());

        Sold(report.Current, RevenueSource.Membership).ShouldBe(2);
        Sold(report.Current, RevenueSource.SingleSession).ShouldBe(3);
        Sold(report.Current, RevenueSource.Cafe).ShouldBe(1);
        Sold(report.Current, RevenueSource.Cardio).ShouldBe(0);
        Money(report.Current, RevenueSource.Membership).Net.ShouldBe(900_000m);

        // The plan sold yesterday is yesterday's sale: the range before counts it.
        Sold(report.Previous, RevenueSource.Membership).ShouldBe(1);
    }

    [Fact]
    public async Task Financial_PaymentOnACancelledPlan_StillCountsInAndBackOut()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var plan = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 900_000m, "Cash");
        await PostOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/refunds",
            new { amount = 900_000m, method = "Cash", reason = "انصراف" });
        await PostOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/cancel", new { reason = "انصراف" });

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        // The money did move, in and back out (§12): received and refunded, nothing net.
        current.Revenue.ShouldBe(new MoneyFlowResponse(900_000m, 900_000m, 0m));
        current.Sales.ShouldBe(0m);
    }

    [Fact]
    public async Task Financial_TwoPeopleTakingMoney_AreEachTheirOwnRow()
    {
        var (owner, ownerToken, ownerId) = await OwnerClientAsync();
        var (staff, staffToken, staffId) = await StaffClientAsync();
        var first = await AssignOkAsync(owner, ownerToken, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, ownerToken, $"/api/subscriptions/{first.Id}/payments", 100_000m, "Cash");
        var second = await AssignOkAsync(owner, ownerToken, (await AddMemberAsync("مریم کاظمی")).Id);
        await PayOkAsync(staff, staffToken, $"/api/subscriptions/{second.Id}/payments", 400_000m, "Card");
        await PostOkAsync(owner, ownerToken, $"/api/subscriptions/{second.Id}/refunds",
            new { amount = 50_000m, method = "Card", reason = "اشتباه در ثبت" });

        var byStaff = (await FinancialOkAsync(owner, ownerToken, TodayRange())).Current.ByStaff;

        // The largest net first; a refund is on whoever gave it back (only the Owner may, §1).
        byStaff.Select(row => row.UserId).ShouldBe([staffId, ownerId]);
        byStaff[0].Money.ShouldBe(new MoneyFlowResponse(400_000m, 0m, 400_000m));
        byStaff[0].FullName.ShouldBe("کاربر تست");
        byStaff[1].Money.ShouldBe(new MoneyFlowResponse(100_000m, 50_000m, 50_000m));
    }

    [Fact]
    public async Task Financial_PaymentsEitherSideOfTheGymsMidnight_LandOnTheGymsDay()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var day = Today().AddDays(-10);
        var nextMidnight = Calendar().StartOfDayUtc(day.AddDays(1));
        var plan = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        var lateEvening = await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 100_000m, "Cash");
        var atMidnight = await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 200_000m, "Cash");
        await MovePaymentAsync(lateEvening.Id, nextMidnight.AddMinutes(-30));
        await MovePaymentAsync(atMidnight.Id, nextMidnight);

        var report = await FinancialOkAsync(owner, token, Range(day, day));

        report.Current.Revenue.Net.ShouldBe(100_000m);
        report.Days.ShouldBe([new FinancialDayResponse(day, 100_000m, 0m)]);
        (await FinancialOkAsync(owner, token, Range(day.AddDays(1), day.AddDays(1)))).Current.Revenue.Net.ShouldBe(200_000m);
    }

    // ---- Financial report: expenses and profit ----

    [Fact]
    public async Task Financial_Expenses_ByCategoryWithVoidedLeftOut_GiveNetProfit()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var plan = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 900_000m, "Card");
        await WalkInOrderAsync(owner, token, 60_000m);
        await RecordExpenseOkAsync(owner, token, 500_000m, RentId, Today());
        await RecordExpenseOkAsync(owner, token, 20_000m, ExpenseCategory.CafePurchasingId, Today());
        await RecordExpenseOkAsync(owner, token, 5_000m, ExpenseCategory.CafePurchasingId, Today());
        var voided = await RecordExpenseOkAsync(owner, token, 300_000m, RentId, Today());
        await PostOkAsync(owner, token, $"/api/expenses/{voided.Id}/void", new { reason = "تکراری" });

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        current.Expenses.ShouldBe(525_000m);
        current.ExpensesByCategory.ShouldBe(
        [
            new ExpensesByCategoryResponse(RentId, "اجاره", 500_000m),
            new ExpensesByCategoryResponse(ExpenseCategory.CafePurchasingId, "خرید بوفه", 25_000m),
        ]);
        current.NetProfit.ShouldBe(960_000m - 525_000m);
    }

    [Fact]
    public async Task Financial_NetProfit_LeavesTheShopAndAnalysisMoneyOutButEveryExpenseIn()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var visitor = await AddMemberAsync("رضا کریمی");
        var plan = await AssignOkAsync(owner, token, visitor.Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 900_000m, "Card");
        var visit = await TestLockers.CheckInOkAsync(owner, token, visitor.Id);
        var shop = await RecordShopOkAsync(owner, token, visit.Id, "دستکش", 200_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{shop.Id}/payments", 200_000m, "Cash");
        var analysis = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 80_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{analysis.Id}/payments", 80_000m, "Cash");
        await RecordExpenseOkAsync(owner, token, 500_000m, RentId, Today());

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        // Every payment is still revenue, shop and analysis included.
        current.Revenue.Net.ShouldBe(1_180_000m);
        // §12 (1405/07/14): the plan's money minus every expense; the shop and analysis are left out.
        current.NetProfit.ShouldBe(900_000m - 500_000m);
    }

    [Fact]
    public async Task Financial_PreviousRange_IsTheSameLengthEndingTheDayBefore()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var today = Today();
        var plan = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        var now = await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 100_000m, "Cash");
        var before = await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 300_000m, "Cash");
        var tooOld = await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 50_000m, "Cash");
        await MovePaymentAsync(before.Id, Calendar().StartOfDayUtc(today.AddDays(-5)).AddHours(12));
        await MovePaymentAsync(tooOld.Id, Calendar().StartOfDayUtc(today.AddDays(-6)).AddHours(12));
        await RecordExpenseOkAsync(owner, token, 70_000m, RentId, today.AddDays(-3));

        var report = await FinancialOkAsync(owner, token, Range(today.AddDays(-2), today));

        report.Current.Revenue.Net.ShouldBe(now.Amount);
        report.Previous.From.ShouldBe(today.AddDays(-5));
        report.Previous.To.ShouldBe(today.AddDays(-3));
        report.Previous.Revenue.Net.ShouldBe(300_000m);
        report.Previous.Expenses.ShouldBe(70_000m);
        report.Previous.NetProfit.ShouldBe(230_000m);
    }

    [Fact]
    public async Task Financial_Days_ListEveryDayOfTheRangeWithZerosWhereNothingHappened()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var today = Today();
        var plan = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 100_000m, "Cash");
        await RecordExpenseOkAsync(owner, token, 40_000m, RentId, today.AddDays(-3));

        var days = (await FinancialOkAsync(owner, token, Range(today.AddDays(-6), today))).Days;

        days.Count.ShouldBe(7);
        days.Select(row => row.Date).ShouldBe(Enumerable.Range(0, 7).Select(offset => today.AddDays(offset - 6)));
        days[3].ShouldBe(new FinancialDayResponse(today.AddDays(-3), 0m, 40_000m));
        days[6].ShouldBe(new FinancialDayResponse(today, 100_000m, 0m));
        days.Sum(row => row.Revenue).ShouldBe(100_000m);
    }

    [Fact]
    public async Task Financial_NothingInTheRange_ReturnsZerosAndEmptyLists()
    {
        var (owner, token, _) = await OwnerClientAsync();

        var current = (await FinancialOkAsync(owner, token, TodayRange())).Current;

        current.Revenue.ShouldBe(new MoneyFlowResponse(0m, 0m, 0m));
        current.BySource.ShouldAllBe(row => row.Money.Net == 0m);
        current.ByMethod.Count.ShouldBe(3);
        current.ByStaff.ShouldBeEmpty();
        current.ExpensesByCategory.ShouldBeEmpty();
        current.NetProfit.ShouldBe(0m);
    }

    // ---- Financial report: who and which range ----

    [Fact]
    public async Task Financial_Staff_Returns403()
    {
        var (staff, token, _) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, $"/api/reports/financial?{TodayRange()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("to=2026-10-04", "from", "Reports.DateRangeRequired")]
    [InlineData("from=2026-10-04", "to", "Reports.DateRangeRequired")]
    [InlineData("from=2026-10-05&to=2026-10-04", "to", "Reports.InvalidDateRange")]
    [InlineData("from=2025-10-03&to=2026-10-04", "to", "Reports.RangeTooLong")]
    public async Task Financial_InvalidRange_Returns400WithItsCode(string query, string field, string code)
    {
        var (owner, token, _) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, $"/api/reports/financial?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    [Fact]
    public async Task Financial_RangeOf366Days_IsAccepted()
    {
        var (owner, token, _) = await OwnerClientAsync();

        var report = await FinancialOkAsync(owner, token, "from=2025-10-04&to=2026-10-04");

        report.Days.Count.ShouldBe(366);
        report.Previous.To.ShouldBe(new DateOnly(2025, 10, 3));
    }

    // ---- Receivables ----

    [Fact]
    public async Task Receivables_OwedSales_AreSplitByAgeAndAddUpToTheTotal()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var today = Today();

        await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id, price: 900_000m);
        var weekOld = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 800_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{weekOld.Id}/payments", 300_000m, "Cash");
        var eightDays = await AssignOkAsync(owner, token, (await AddMemberAsync("رضا کریمی")).Id, price: 700_000m);
        var thirtyDays = await AssignOkAsync(owner, token, (await AddMemberAsync("زهرا امینی")).Id, price: 600_000m);
        var older = await AssignOkAsync(owner, token, (await AddMemberAsync("حسن نوری")).Id, price: 500_000m);
        var paid = await AssignOkAsync(owner, token, (await AddMemberAsync("سارا احمدی")).Id, price: 300_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{paid.Id}/payments", 300_000m, "Card");
        var cancelled = await AssignOkAsync(owner, token, (await AddMemberAsync("نرگس رحیمی")).Id, price: 400_000m);
        await PostOkAsync(owner, token, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "انصراف" });

        // Each at its age's edge: the first minute of the gym's day 7, 8, 30 and 31 days ago.
        await MoveSubscriptionSaleAsync(weekOld.Id, Calendar().StartOfDayUtc(today.AddDays(-7)));
        await MoveSubscriptionSaleAsync(eightDays.Id, Calendar().StartOfDayUtc(today.AddDays(-7)).AddMinutes(-1));
        await MoveSubscriptionSaleAsync(thirtyDays.Id, Calendar().StartOfDayUtc(today.AddDays(-30)));
        await MoveSubscriptionSaleAsync(older.Id, Calendar().StartOfDayUtc(today.AddDays(-30)).AddMinutes(-1));

        var receivables = await GetOkAsync<ReceivablesResponse>(owner, token, "/api/reports/receivables");

        receivables.ShouldBe(new ReceivablesResponse(
            Total: 3_200_000m, UpTo7Days: 1_400_000m, From8To30Days: 1_300_000m, Over30Days: 500_000m));
    }

    [Fact]
    public async Task Receivables_Total_IsWhatEverySaleStillOwesInTheHistory()
    {
        var (owner, token, _) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var plan = await AssignOkAsync(owner, token, member.Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 800_000m, "Cash");
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await MoveSubscriptionSaleAsync(plan.Id, Calendar().StartOfDayUtc(Today().AddDays(-40)));

        var receivables = await GetOkAsync<ReceivablesResponse>(owner, token, "/api/reports/receivables");
        var history = await GetOkAsync<SalesTotalsResponse>(owner, token, "/api/sales/totals");

        receivables.Total.ShouldBe(history.Remaining);
        receivables.ShouldBe(new ReceivablesResponse(150_000m, 50_000m, 0m, 100_000m));
    }

    [Fact]
    public async Task Receivables_NothingOwed_ReturnsZeros()
    {
        var (owner, token, _) = await OwnerClientAsync();

        var receivables = await GetOkAsync<ReceivablesResponse>(owner, token, "/api/reports/receivables");

        receivables.ShouldBe(new ReceivablesResponse(0m, 0m, 0m, 0m));
    }

    [Fact]
    public async Task Receivables_Staff_Returns403()
    {
        var (staff, token, _) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, "/api/reports/receivables");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Helpers ----

    private static MoneyFlowResponse Money(FinancialPeriodResponse period, RevenueSource source) =>
        period.BySource.Single(row => row.Source == source).Money;

    private static int Sold(FinancialPeriodResponse period, RevenueSource source) =>
        period.BySource.Single(row => row.Source == source).Sold;

    private async Task<(HttpClient Client, string Token, Guid UserId)> StaffClientAsync()
    {
        var user = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password), user.Id);
    }

    private async Task<(HttpClient Client, string Token, Guid UserId)> OwnerClientAsync()
    {
        var user = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password), user.Id);
    }

    private IGymCalendar Calendar() => Fixture.Services.GetRequiredService<IGymCalendar>();

    private DateOnly Today() => Calendar().Today();

    private string TodayRange() => Range(Today(), Today());

    private static string Range(DateOnly from, DateOnly to) => $"from={Iso(from)}&to={Iso(to)}";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98915{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<SubscriptionResponse> AssignOkAsync(
        HttpClient client, string token, Guid memberId, decimal price = 900_000m)
    {
        var plan = await TestPlans.AddAsync(Fixture, price: price);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<SubscriptionResponse> SellSingleVisitOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/single-visit", body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ServiceChargeResponse> RecordChargeOkAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind, amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>One فروشگاه line of one item, left owed: a sale takes no money when it is recorded (§7).</summary>
    private static async Task<ServiceChargeResponse> RecordShopOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, decimal unitPrice)
    {
        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/attendance/{attendanceId}/service-charges/shop",
            new { items = new[] { new { description, quantity = 1, unitPrice } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var charges = (await response.Content.ReadFromJsonAsync<List<ServiceChargeResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return charges.ShouldHaveSingleItem();
    }

    /// <summary>A walk-in's cafe order of one product, paid in full in cash at the till.</summary>
    private static async Task WalkInOrderAsync(HttpClient client, string token, decimal price)
    {
        using var category = await SendAsync(client, token, HttpMethod.Post, "/api/cafe/categories", new { name = "نوشیدنی" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;

        using var product = await SendAsync(
            client, token, HttpMethod.Post, "/api/cafe/products", new { name = "آب معدنی", categoryId, price });
        product.EnsureSuccessStatusCode();
        var productId = (await product.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;

        using var order = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId = (Guid?)null,
                attendanceId = (Guid?)null,
                items = new[] { new { productId, quantity = 1 } },
                payment = new { amount = price, method = "Cash", referenceNumber = (string?)null },
            });
        order.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<ExpenseResponse> RecordExpenseOkAsync(
        HttpClient client, string token, decimal amount, Guid categoryId, DateOnly expenseDate)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, "/api/expenses", new { amount, categoryId, expenseDate, description = "هزینه" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<Application.Payments.PaymentResponse> PayOkAsync(
        HttpClient client, string token, string path, decimal amount, string method)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<Application.Payments.PaymentResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<FinancialReportResponse> FinancialOkAsync(HttpClient client, string token, string query) =>
        GetOkAsync<FinancialReportResponse>(client, token, $"/api/reports/financial?{query}");

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.IsSuccessStatusCode.ShouldBeTrue($"POST {path} answered {(int)response.StatusCode}.");
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await GetAsync(client, token, path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Get, path, body: null);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private async Task MovePaymentAsync(Guid paymentId, DateTimeOffset paidAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE payments SET paid_at = {paidAt} WHERE id = {paymentId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveSubscriptionSaleAsync(Guid subscriptionId, DateTimeOffset createdAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET created_at = {createdAt} WHERE id = {subscriptionId}",
            TestContext.Current.CancellationToken);
    }
}

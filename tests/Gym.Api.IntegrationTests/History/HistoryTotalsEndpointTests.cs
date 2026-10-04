using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.History.ListPayments;
using Gym.Application.History.ListSales;
using Gym.Application.History.PaymentTotals;
using Gym.Application.History.SalesTotals;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.History;

/// <summary>
/// The totals under the history's sections: <c>GET /api/sales/totals</c> and
/// <c>GET /api/payments/totals</c> (BUSINESS_RULES.md §12 <i>Totals in the history</i>, roadmap
/// 6.5.32). Several tests also add up the list's own rows, to show the totals count exactly what
/// the list shows.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class HistoryTotalsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Sales ----

    [Fact]
    public async Task SalesTotals_AllFiveKinds_AddUpAmountNetPaidAndWhatIsStillOwed()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var plan = await AssignOkAsync(owner, token, member.Id, price: 900_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{plan.Id}/payments", 100_000m, "Cash");
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "Card");
        await RecordShopOkAsync(owner, token, visit.Id, "دستکش", quantity: 2, unitPrice: 300_000m);
        await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        await WalkInOrderAsync(owner, token, 60_000m);

        var totals = await SalesTotalsOkAsync(owner, token, TodayRange());

        // 900 + 50 + 600 + 200 + 60; paid 100 + 50 + 60; owed 800 + 600 + 200.
        totals.ShouldBe(new SalesTotalsResponse(1_810_000m, 210_000m, 1_600_000m));
    }

    [Fact]
    public async Task SalesTotals_EachSource_AddsUpOnlyThatKind()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(owner, token, member.Id, price: 900_000m);
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await RecordShopOkAsync(owner, token, visit.Id, "دستکش", quantity: 1, unitPrice: 300_000m);
        await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        await WalkInOrderAsync(owner, token, 60_000m);

        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&source=Subscription")).Amount.ShouldBe(900_000m);
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&source=Cardio")).Amount.ShouldBe(50_000m);
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&source=Miscellaneous")).Amount.ShouldBe(300_000m);
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&source=Analysis")).Amount.ShouldBe(200_000m);
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&source=CafeOrder"))
            .ShouldBe(new SalesTotalsResponse(60_000m, 60_000m, 0m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&paid=Paid")]
    [InlineData("&paid=Unpaid")]
    public async Task SalesTotals_PaidFilter_AddUpExactlyTheListsLiveRows(string paid)
    {
        var (owner, token) = await OwnerClientAsync();
        var paidInFull = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id, price: 900_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{paidInFull.Id}/payments", 900_000m, "Card");
        var partlyPaid = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 800_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{partlyPaid.Id}/payments", 100_000m, "Cash");
        await AssignOkAsync(owner, token, (await AddMemberAsync("رضا کریمی")).Id, price: 700_000m);
        await AssignOkAsync(owner, token, (await AddMemberAsync("زهرا امینی")).Id, price: 0m);

        var totals = await SalesTotalsOkAsync(owner, token, TodayRange() + paid);
        var list = await SalesOkAsync(owner, token, TodayRange() + paid);

        var live = list.Items.Where(item => item.UndoneAt is null).ToList();
        totals.Amount.ShouldBe(live.Sum(item => item.Amount));
        totals.NetPaid.ShouldBe(live.Sum(item => item.NetPaid));
        totals.Remaining.ShouldBe(live.Sum(item => item.Amount - item.NetPaid));
    }

    [Fact]
    public async Task SalesTotals_PaidAndUnpaid_SplitTheFiguresBetweenThem()
    {
        var (owner, token) = await OwnerClientAsync();
        var paidInFull = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id, price: 900_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{paidInFull.Id}/payments", 900_000m, "Card");
        var partlyPaid = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 800_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{partlyPaid.Id}/payments", 100_000m, "Cash");
        await AssignOkAsync(owner, token, (await AddMemberAsync("رضا کریمی")).Id, price: 700_000m);
        await AssignOkAsync(owner, token, (await AddMemberAsync("زهرا امینی")).Id, price: 0m);

        // A free plan is paid and adds nothing (§4); a partial payment still owes (§12).
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&paid=Paid"))
            .ShouldBe(new SalesTotalsResponse(900_000m, 900_000m, 0m));
        (await SalesTotalsOkAsync(owner, token, TodayRange() + "&paid=Unpaid"))
            .ShouldBe(new SalesTotalsResponse(1_500_000m, 100_000m, 1_400_000m));
        (await SalesTotalsOkAsync(owner, token, TodayRange()))
            .ShouldBe(new SalesTotalsResponse(2_400_000m, 1_000_000m, 1_400_000m));
    }

    [Fact]
    public async Task SalesTotals_CancelledPlanAndVoidedCharge_CountTowardNone()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(owner, token, member.Id, price: 900_000m);
        var cancelled = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 800_000m);
        await PostOkAsync(owner, token, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "انصراف" });
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var voided = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{voided.Id}/payments", 50_000m, "Cash");
        await PostOkAsync(owner, token, $"/api/service-charges/{voided.Id}/void", new { reason = "مبلغ اشتباه" });
        await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 40_000m);

        var totals = await SalesTotalsOkAsync(owner, token, TodayRange());
        var list = await SalesOkAsync(owner, token, TodayRange());

        // Both stay listed under «همه», marked; neither adds to the figures.
        list.TotalCount.ShouldBe(4);
        totals.ShouldBe(new SalesTotalsResponse(940_000m, 0m, 940_000m));
    }

    [Fact]
    public async Task SalesTotals_DateRangeAndMember_FollowTheListsFilters()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(owner, token, member.Id, price: 900_000m);
        var earlier = await AssignOkAsync(owner, token, member.Id, price: 800_000m);
        await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id, price: 700_000m);
        await WalkInOrderAsync(owner, token, 60_000m);
        await MoveSubscriptionSaleAsync(earlier.Id, new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));

        var mineToday = await SalesTotalsOkAsync(owner, token, TodayRange() + $"&memberId={member.Id}");
        var mineEver = await SalesTotalsOkAsync(owner, token, $"memberId={member.Id}");
        var everyoneToday = await SalesTotalsOkAsync(owner, token, TodayRange());

        mineToday.Amount.ShouldBe(900_000m);
        mineEver.Amount.ShouldBe(1_700_000m);
        everyoneToday.Amount.ShouldBe(1_660_000m);
    }

    [Fact]
    public async Task SalesTotals_NothingSold_ReturnsZeros()
    {
        var (owner, token) = await OwnerClientAsync();

        var totals = await SalesTotalsOkAsync(owner, token, TodayRange());

        totals.ShouldBe(new SalesTotalsResponse(0m, 0m, 0m));
    }

    [Fact]
    public async Task SalesTotals_Staff_Returns403()
    {
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, $"/api/sales/totals?{TodayRange()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("paid=Partial")]
    [InlineData("source=Expense")]
    [InlineData("from=2026-09-12&to=2026-09-10")]
    public async Task SalesTotals_InvalidFilter_Returns400(string query)
    {
        var (owner, token) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, $"/api/sales/totals?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Payments ----

    [Fact]
    public async Task PaymentTotals_PaymentsAndRefunds_GiveReceivedRefundedAndNet()
    {
        var (owner, token) = await OwnerClientAsync();
        var subscription = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id, price: 900_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 900_000m, "Cash");
        await PostOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/refunds",
            new { amount = 900_000m, method = "Cash", reason = "اشتباه در ثبت" });
        await PostOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/cancel", new { reason = "انصراف" });
        await WalkInOrderAsync(owner, token, 60_000m);

        var totals = await PaymentTotalsOkAsync(owner, token, TodayRange());

        // The cancelled plan's money did move, in and back out: both count (§12 Totals).
        totals.ShouldBe(new PaymentTotalsResponse(960_000m, 900_000m, 60_000m));
    }

    [Fact]
    public async Task PaymentTotals_ByMethodSourceKindAndMember_AddUpExactlyTheListsRows()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var other = await AddMemberAsync("مریم کاظمی");
        var mine = await AssignOkAsync(owner, token, member.Id, price: 900_000m);
        var theirs = await AssignOkAsync(owner, token, other.Id, price: 900_000m);
        await PayOkAsync(owner, token, $"/api/subscriptions/{mine.Id}/payments", 100_000m, "Card");
        await PayOkAsync(owner, token, $"/api/subscriptions/{theirs.Id}/payments", 200_000m, "Cash");
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "Cash");
        var analysis = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        await PayOkAsync(owner, token, $"/api/service-charges/{analysis.Id}/payments", 200_000m, "Card");
        await WalkInOrderAsync(owner, token, 30_000m);

        (await PaymentTotalsOkAsync(owner, token, TodayRange() + "&method=Cash")).Received.ShouldBe(280_000m);
        (await PaymentTotalsOkAsync(owner, token, TodayRange() + "&source=ServiceCharge")).Received.ShouldBe(250_000m);
        (await PaymentTotalsOkAsync(owner, token, TodayRange() + "&source=ServiceCharge&serviceKind=Cardio"))
            .Received.ShouldBe(50_000m);
        (await PaymentTotalsOkAsync(owner, token, TodayRange() + $"&memberId={member.Id}")).Received.ShouldBe(350_000m);

        foreach (var filter in new[] { "", "&method=Card", "&source=CafeOrder", $"&memberId={other.Id}" })
        {
            var totals = await PaymentTotalsOkAsync(owner, token, TodayRange() + filter);
            var list = await PaymentsOkAsync(owner, token, TodayRange() + filter);
            totals.Net.ShouldBe(list.Items.Sum(item => item.Kind == PaymentKind.Payment ? item.Amount : -item.Amount));
        }
    }

    [Fact]
    public async Task PaymentTotals_OwnerWithNoDates_AddsUpEveryDay()
    {
        var (owner, token) = await OwnerClientAsync();
        var subscription = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id, price: 900_000m);
        var old = await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 100_000m, "Cash");
        await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 200_000m, "Cash");
        await MovePaymentAsync(old.Id, new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));

        (await PaymentTotalsOkAsync(owner, token, "")).Received.ShouldBe(300_000m);
        (await PaymentTotalsOkAsync(owner, token, TodayRange())).Received.ShouldBe(200_000m);
    }

    [Fact]
    public async Task PaymentTotals_StaffEvenForToday_Returns403()
    {
        // Staff see today's payments but not their totals (§1, §12 Totals in the history).
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, $"/api/payments/totals?{TodayRange()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("method=Bitcoin")]
    [InlineData("source=Expense")]
    [InlineData("from=2026-09-12&to=2026-09-10")]
    public async Task PaymentTotals_InvalidFilter_Returns400(string query)
    {
        var (owner, token) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, $"/api/payments/totals?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Totals_WithoutSigningIn_Return401()
    {
        var client = Fixture.CreateClient();

        using var sales = await client.GetAsync("/api/sales/totals", TestContext.Current.CancellationToken);
        using var payments = await client.GetAsync("/api/payments/totals", TestContext.Current.CancellationToken);

        sales.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        payments.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

    private string TodayRange() => $"from={Iso(Today())}&to={Iso(Today())}";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98914{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId, decimal price)
    {
        var plan = await TestPlans.AddAsync(Fixture, price: price);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
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

    /// <summary>One فروشگاه line, left owed: a sale takes no money when it is recorded (§7).</summary>
    private static async Task RecordShopOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice)
    {
        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/attendance/{attendanceId}/service-charges/shop",
            new { items = new[] { new { description, quantity, unitPrice } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    private static async Task<Application.Payments.PaymentResponse> PayOkAsync(
        HttpClient client, string token, string path, decimal amount, string method)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<Application.Payments.PaymentResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<SalesTotalsResponse> SalesTotalsOkAsync(HttpClient client, string token, string query) =>
        GetOkAsync<SalesTotalsResponse>(client, token, $"/api/sales/totals?{query}");

    private static Task<PagedResponse<HistorySaleResponse>> SalesOkAsync(HttpClient client, string token, string query) =>
        GetOkAsync<PagedResponse<HistorySaleResponse>>(client, token, $"/api/sales?{query}");

    private static Task<PaymentTotalsResponse> PaymentTotalsOkAsync(HttpClient client, string token, string query) =>
        GetOkAsync<PaymentTotalsResponse>(client, token, $"/api/payments/totals?{query}");

    private static Task<PagedResponse<HistoryPaymentResponse>> PaymentsOkAsync(HttpClient client, string token, string query) =>
        GetOkAsync<PagedResponse<HistoryPaymentResponse>>(client, token, $"/api/payments?{query}");

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

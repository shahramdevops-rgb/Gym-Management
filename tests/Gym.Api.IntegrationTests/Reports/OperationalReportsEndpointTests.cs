using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetMembersReport;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Application.Reports.GetReceivables;
using Gym.Application.Reports.GetSubscriptionsSnapshot;
using Gym.Application.Reports.GetTopCafeProducts;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Reports;

/// <summary>
/// The operational reports through the API, with the real clock: BUSINESS_RULES.md §12
/// <i>Operational reports</i>, <i>Needs attention</i> (roadmap 9.2). The rules that turn on dates
/// are tested against a pinned clock in <see cref="OperationalReportsTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class OperationalReportsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Attendance ----

    [Fact]
    public async Task Attendance_RealCheckInAndGuest_CountsTheMemberOnly()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(owner, token, member.Id);
        await TestLockers.CheckInOkAsync(owner, token, member.Id);
        await TestGuests.CheckInOkAsync(owner, token, lockerNumber: 2);

        var report = await GetOkAsync<AttendanceReportResponse>(owner, token, $"/api/reports/attendance?{TodayRange()}");

        report.Visits.ShouldBe(1);
        report.Members.ShouldBe(1);
        report.Days.ShouldBe([new AttendanceDayResponse(Today(), 1)]);
        report.ByWeekday.Count.ShouldBe(7);
        report.ByWeekday.Sum(row => row.Hours.Sum()).ShouldBe(1);
    }

    // ---- Top cafe products ----

    [Fact]
    public async Task CafeProducts_RankedByQuantityWithCancelledOrdersLeftOut()
    {
        var (owner, token) = await OwnerClientAsync();
        var categoryId = await AddCategoryAsync(owner, token);
        var water = await AddProductAsync(owner, token, categoryId, "آب معدنی", 20_000m);
        var juice = await AddProductAsync(owner, token, categoryId, "آبمیوه", 50_000m);
        var bar = await AddProductAsync(owner, token, categoryId, "شکلات", 30_000m);
        await WalkInOrderAsync(owner, token, (water, 20_000m, 3), (juice, 50_000m, 1));
        await WalkInOrderAsync(owner, token, (water, 20_000m, 1), (bar, 30_000m, 2));
        var cancelled = await WalkInOrderAsync(owner, token, (juice, 50_000m, 5));
        await PostOkAsync(owner, token, $"/api/cafe/orders/{cancelled}/cancel", new { reason = "اشتباه در ثبت" });

        var top = await GetOkAsync<List<TopCafeProductResponse>>(owner, token, $"/api/reports/cafe-products?{TodayRange()}");

        top.ShouldBe(
        [
            new TopCafeProductResponse(water, "آب معدنی", 4, 80_000m),
            new TopCafeProductResponse(bar, "شکلات", 2, 60_000m),
            new TopCafeProductResponse(juice, "آبمیوه", 1, 50_000m),
        ]);
    }

    [Fact]
    public async Task CafeProducts_MoreThanTen_ListsTheTopTen()
    {
        var (owner, token) = await OwnerClientAsync();
        var categoryId = await AddCategoryAsync(owner, token);
        var lines = new List<(Guid ProductId, decimal Price, int Quantity)>();
        for (var quantity = 1; quantity <= GetTopCafeProductsHandler.Count + 1; quantity++)
        {
            lines.Add((await AddProductAsync(owner, token, categoryId, $"کالا {quantity}", 10_000m), 10_000m, quantity));
        }

        await WalkInOrderAsync(owner, token, [.. lines]);

        var top = await GetOkAsync<List<TopCafeProductResponse>>(owner, token, $"/api/reports/cafe-products?{TodayRange()}");

        top.Count.ShouldBe(GetTopCafeProductsHandler.Count);
        top[0].Quantity.ShouldBe(11);
        top.ShouldNotContain(row => row.Quantity == 1);
    }

    // ---- Subscriptions, members, needs attention ----

    [Fact]
    public async Task Subscriptions_AnActivePlan_IsCounted()
    {
        var (owner, token) = await OwnerClientAsync();
        await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);

        var snapshot = await GetOkAsync<SubscriptionsSnapshotResponse>(owner, token, "/api/reports/subscriptions");

        snapshot.ShouldBe(new SubscriptionsSnapshotResponse(Today(), Active: 1, Frozen: 0, ExpiringSoon: 0, LowSessions: 0));
    }

    [Fact]
    public async Task Members_APlanSoldToday_MakesANewMember()
    {
        var (owner, token) = await OwnerClientAsync();
        await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);

        var report = await GetOkAsync<MembersReportResponse>(owner, token, $"/api/reports/members?{TodayRange()}");

        report.NewMembers.ShouldBe(1);
        report.Ended.ShouldBe(0);
        report.Days.ShouldBe([new MembersDayResponse(Today(), 0, 0, 0, 1)]);
    }

    [Fact]
    public async Task NeedsAttention_OldDebts_AddUpToTheReceivablesOldestAge()
    {
        var (owner, token) = await OwnerClientAsync();
        var today = Today();
        var old = await AddMemberAsync("علی بدهکار قدیمی");
        var oldPlan = await AssignOkAsync(owner, token, old.Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{oldPlan.Id}/payments", 100_000m);
        await MoveSubscriptionSaleAsync(oldPlan.Id, Calendar().StartOfDayUtc(today.AddDays(-40)));
        var edge = await AddMemberAsync("مریم سی‌ویک‌روز");
        var edgePlan = await AssignOkAsync(owner, token, edge.Id);
        await MoveSubscriptionSaleAsync(edgePlan.Id, Calendar().StartOfDayUtc(today.AddDays(-30)).AddMinutes(-1));
        var recent = await AddMemberAsync("رضا بدهکار تازه");
        var recentPlan = await AssignOkAsync(owner, token, recent.Id);
        await MoveSubscriptionSaleAsync(recentPlan.Id, Calendar().StartOfDayUtc(today.AddDays(-30)));
        var guest = await TestGuests.CheckInOkAsync(owner, token, lockerNumber: 3);
        var guestCardio = await RecordCardioOkAsync(owner, token, guest.Id, 70_000m);
        await MoveChargeSaleAsync(guestCardio.Id, Calendar().StartOfDayUtc(today.AddDays(-35)));

        var needs = await GetOkAsync<NeedsAttentionResponse>(owner, token, "/api/reports/needs-attention");
        var receivables = await GetOkAsync<ReceivablesResponse>(owner, token, "/api/reports/receivables");

        // The largest first; 30 days old to the minute is not old yet, a minute more is.
        needs.OldDebts.ShouldBe(
        [
            new OldDebtResponse(edge.Id, edge.FullName, edge.PhoneNumber, 900_000m, today.AddDays(-31)),
            new OldDebtResponse(old.Id, old.FullName, old.PhoneNumber, 800_000m, today.AddDays(-40)),
        ]);
        needs.OldDebtWithoutMember.ShouldBe(70_000m);
        (needs.OldDebts.Sum(row => row.Owed) + needs.OldDebtWithoutMember).ShouldBe(receivables.Over30Days);
    }

    // ---- Who and which range ----

    [Theory]
    [InlineData("/api/reports/attendance?from=2026-10-04&to=2026-10-04")]
    [InlineData("/api/reports/subscriptions")]
    [InlineData("/api/reports/cafe-products?from=2026-10-04&to=2026-10-04")]
    [InlineData("/api/reports/needs-attention")]
    [InlineData("/api/reports/members?from=2026-10-04&to=2026-10-04")]
    public async Task Report_Staff_Returns403(string path)
    {
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("attendance", "to=2026-10-04", "from", "Reports.DateRangeRequired")]
    [InlineData("attendance", "from=2026-10-05&to=2026-10-04", "to", "Reports.InvalidDateRange")]
    [InlineData("cafe-products", "from=2026-10-04", "to", "Reports.DateRangeRequired")]
    [InlineData("cafe-products", "from=2025-10-03&to=2026-10-04", "to", "Reports.RangeTooLong")]
    [InlineData("members", "to=2026-10-04", "from", "Reports.DateRangeRequired")]
    [InlineData("members", "from=2025-10-03&to=2026-10-04", "to", "Reports.RangeTooLong")]
    public async Task Report_InvalidRange_Returns400WithItsCode(string report, string query, string field, string code)
    {
        var (owner, token) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, $"/api/reports/{report}?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private IGymCalendar Calendar() => Fixture.Services.GetRequiredService<IGymCalendar>();

    private DateOnly Today() => Calendar().Today();

    private string TodayRange()
    {
        var today = Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"from={today}&to={today}";
    }

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98917{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>The default plan: 10 sessions, 30 days, 900,000.</summary>
    private async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId)
    {
        var plan = await TestPlans.AddAsync(Fixture);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayOkAsync(HttpClient client, string token, string path, decimal amount)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method = "Cash" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<ServiceChargeResponse> RecordCardioOkAsync(
        HttpClient client, string token, Guid attendanceId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind = "Cardio", amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<Guid> AddCategoryAsync(HttpClient client, string token)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, "/api/cafe/categories", new { name = "خوراکی" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;
    }

    private static async Task<Guid> AddProductAsync(
        HttpClient client, string token, Guid categoryId, string name, decimal price)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, "/api/cafe/products", new { name, categoryId, price });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;
    }

    /// <summary>A walk-in's order, paid in full in cash at the till as a walk-in's must be (§8).</summary>
    private static async Task<Guid> WalkInOrderAsync(
        HttpClient client, string token, params (Guid ProductId, decimal Price, int Quantity)[] lines)
    {
        using var order = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId = (Guid?)null,
                attendanceId = (Guid?)null,
                items = lines.Select(line => new { productId = line.ProductId, quantity = line.Quantity }).ToArray(),
                payment = new
                {
                    amount = lines.Sum(line => line.Price * line.Quantity),
                    method = "Cash",
                    referenceNumber = (string?)null,
                },
            });
        order.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await order.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;
    }

    private async Task MoveSubscriptionSaleAsync(Guid subscriptionId, DateTimeOffset createdAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET created_at = {createdAt} WHERE id = {subscriptionId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveChargeSaleAsync(Guid chargeId, DateTimeOffset createdAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE service_charges SET created_at = {createdAt} WHERE id = {chargeId}",
            TestContext.Current.CancellationToken);
    }

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
}

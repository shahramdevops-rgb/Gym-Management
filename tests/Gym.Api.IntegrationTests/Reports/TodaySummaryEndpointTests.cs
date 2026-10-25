using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Reports;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Reports.GetTodaySummary;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Reports;

/// <summary>
/// Today's summary for the Owner's phone: <c>GET /api/reports/today</c> (BUSINESS_RULES.md §12
/// <i>Today's summary</i>, roadmap 13.5).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TodaySummaryEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TodayPath = "/api/reports/today";

    private static int _phoneSuffix;

    [Fact]
    public async Task Today_BusyDay_CountsWhoIsInsideAndTheDashboardsFiguresForToday()
    {
        var (client, token) = await OwnerClientAsync();

        // A plan paid partly by card and partly in cash, and the member is still inside.
        var stays = await AddMemberAsync("علی رضایی");
        var plan = await AssignOkAsync(client, token, stays.Id);
        await PayOkAsync(client, token, $"/api/subscriptions/{plan.Id}/payments", 600_000m, "Card");
        await PayOkAsync(client, token, $"/api/subscriptions/{plan.Id}/payments", 300_000m, "Cash");
        await TestLockers.CheckInOkAsync(client, token, stays.Id);

        // A single visit paid in cash: came and has already left, so a visit but not inside.
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var left = await AddMemberAsync("مریم کاظمی");
        var single = await SellSingleVisitOkAsync(client, token, left.Id);
        await PayOkAsync(client, token, $"/api/subscriptions/{single.Id}/payments", 150_000m, "Cash");
        var leftVisit = await TestLockers.CheckInOkAsync(client, token, left.Id);
        await PostOkAsync(client, token, $"/api/attendance/{leftVisit.Id}/check-out", body: null);

        // A check-in made by mistake and cancelled: neither inside nor a visit, though the plan is sold.
        var mistaken = await AddMemberAsync("سارا امینی");
        await AssignOkAsync(client, token, mistaken.Id);
        var mistake = await TestLockers.CheckInOkAsync(client, token, mistaken.Id);
        await PostOkAsync(client, token, $"/api/attendance/{mistake.Id}/cancel", CancelCheckInBody.KeepPurchases);

        // A guest is inside like anyone else, but is no attendance (§7).
        await TestGuests.CheckInOkAsync(client, token, lockerNumber: 10);

        var summary = await GetOkAsync<TodaySummaryResponse>(client, token, TodayPath);

        var today = Today();
        summary.ShouldBe(new TodaySummaryResponse(
            today,
            InsideNow: 2,
            GuestsInside: 1,
            CheckIns: 2,
            Received: 1_050_000m,
            PlansSold: 2,
            SingleVisitsSold: 1));

        // The dashboard says the same about today.
        var query = $"from={Iso(today)}&to={Iso(today)}";
        var financial = (await GetOkAsync<FinancialReportResponse>(client, token, $"/api/reports/financial?{query}")).Current;
        var attendance = await GetOkAsync<AttendanceReportResponse>(client, token, $"/api/reports/attendance?{query}");
        summary.CheckIns.ShouldBe(attendance.Visits);
        summary.Received.ShouldBe(financial.ReceivedByMethod.Sum(row => row.Money.Net));
        summary.PlansSold.ShouldBe(financial.BySource.Single(row => row.Source == RevenueSource.Membership).Sold);
        summary.SingleVisitsSold.ShouldBe(financial.BySource.Single(row => row.Source == RevenueSource.SingleSession).Sold);
    }

    [Fact]
    public async Task Today_VisitStillOpenFromYesterday_IsInsideButNoCheckInToday()
    {
        var (client, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("رضا احمدی");
        await AssignOkAsync(client, token, member.Id);
        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id);

        // Checked in yesterday and not out yet: the nightly job has not closed it. «داخل باشگاه»
        // lists every open visit, whatever its day; «ورود امروز» counts only today's.
        await MoveCheckInBackADayAsync(visit.Id);

        var summary = await GetOkAsync<TodaySummaryResponse>(client, token, TodayPath);

        summary.InsideNow.ShouldBe(1);
        summary.CheckIns.ShouldBe(0);
    }

    [Fact]
    public async Task Today_NothingYet_ReturnsZeros()
    {
        var (client, token) = await OwnerClientAsync();

        var summary = await GetOkAsync<TodaySummaryResponse>(client, token, TodayPath);

        summary.ShouldBe(new TodaySummaryResponse(Today(), 0, 0, 0, 0m, 0, 0));
    }

    [Fact]
    public async Task Today_Staff_Returns403()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await SendAsync(client, token, HttpMethod.Get, TodayPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Today_NoToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(TodayPath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    private async Task MoveCheckInBackADayAsync(Guid visitId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"UPDATE attendances SET checked_in_at = checked_in_at - interval '1 day' WHERE id = {visitId}",
            TestContext.Current.CancellationToken);
    }

    private async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId)
    {
        var plan = await TestPlans.AddAsync(Fixture, price: 900_000m);

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

    private static async Task PayOkAsync(HttpClient client, string token, string path, decimal amount, string method)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.IsSuccessStatusCode.ShouldBeTrue($"POST {path} answered {(int)response.StatusCode}.");
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path, body: null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

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
}

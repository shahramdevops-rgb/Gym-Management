using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// <c>POST /api/members/{memberId}/attendance/check-in</c> with a <c>sale</c>: the desk sells a
/// single visit or a plan in the check-in box and the member goes in with the locker it clicked, in
/// one transaction (BUSINESS_RULES.md §7 <i>Confirming at the front desk</i>, roadmap 6.5.7).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CheckInWithSaleEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const decimal SessionPrice = 75_000m;
    private const decimal SingleVisitPrice = 150_000m;

    private static int _phoneSuffix;

    // ---- A plan ----

    [Fact]
    public async Task CheckIn_PlanSaleForAMemberWithNoSubscription_SellsItFromTodayAndChecksInWithThatLocker()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 12, PlanSale(days: 30, sessions: 12));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.LockerId.ShouldBe(TestLockers.IdOf(12));
        var sold = (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
        attendance.SubscriptionId.ShouldBe(sold.Id);
        sold.IsSingleSession.ShouldBeFalse();
        sold.StartDate.ShouldBe(Today());
        sold.EndDate.ShouldBe(Today().AddDays(29));
        sold.TotalSessions.ShouldBe(12);
        sold.UsedSessions.ShouldBe(1);
        sold.Price.ShouldBe(12 * SessionPrice);
    }

    [Fact]
    public async Task CheckIn_PlanSaleAfterAnExpiredMembership_StartsTodayAndChecksIn()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, today.AddDays(-40), today.AddDays(-10));

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 3, PlanSale(days: 30, sessions: 8));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sold = (await StoredSubscriptionsAsync(member.Id)).Single(s => s.StartDate == today);
        sold.UsedSessions.ShouldBe(1);
        (await ReadAsync(response)).SubscriptionId.ShouldBe(sold.Id);
    }

    [Fact]
    public async Task CheckIn_PlanSaleButTheLockerIsTaken_Returns409AndSellsNothing()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var holder = await AddMemberAsync();
        using (var first = await CheckInAsync(client, token, holder.Id, lockerNumber: 7, SingleVisitSale()))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 7, PlanSale(days: 30, sessions: 12));

        // A subscription sold at the locker always comes with that locker: no locker, no sale.
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockerTaken");
        (await StoredSubscriptionsAsync(member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckIn_PlanSaleQueuedBehindAFrozenMembership_Returns422AndSellsNothing()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, today.AddDays(-5), today.AddDays(24), frozenSince: today.AddDays(-1));

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 12));

        // The new plan would start after the frozen one ends, so the member still cannot come in
        // today, and the sale goes back with the refused check-in.
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CheckIn_PlanSaleWhenTheMembershipRanOutOnItsFirstDay_Returns422NextStartsTomorrowAndSellsNothing()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, today, today.AddDays(29), usedSessions: 12);

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 12));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NextStartsTomorrow");
        (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CheckIn_PlanSaleWhileTheSessionPriceIsNotSet_Returns422SessionPriceNotSet()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 12));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Pricing.SessionPriceNotSet");
        (await StoredSubscriptionsAsync(member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckIn_PlanSaleWithTooFewSessions_Returns400OnTheSessionCountField()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 4));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "sessionCount")).ShouldBe("Subscriptions.SessionCountTooLow");
    }

    [Fact]
    public async Task CheckIn_PlanSaleWithNoDays_Returns400OnTheDurationDaysField()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(
            client, token, member.Id, lockerNumber: 1, new { kind = "Membership", sessionCount = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "durationDays")).ShouldBe("Subscriptions.DurationInvalid");
    }

    [Fact]
    public async Task CheckIn_PlanSaleForAnInactiveMember_Returns422MembersInactive()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync(active: false);

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 12));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
        (await StoredSubscriptionsAsync(member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelCheckIn_AfterAPlanSale_KeepsThePlanWithItsSessionBack()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();
        using var checkIn = await CheckInAsync(client, token, member.Id, lockerNumber: 1, PlanSale(days: 30, sessions: 12));
        var attendance = await ReadAsync(checkIn);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendance.Id}/cancel", CancelCheckInBody.KeepPurchases);

        // The only way a plan sold at the locker ends up without a visit: the visit is undone, the
        // plan stays for next time.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
        plan.CancelledAt.ShouldBeNull();
        plan.UsedSessions.ShouldBe(0);
    }

    // ---- A single visit ----

    [Fact]
    public async Task CheckIn_SingleVisitSale_SellsOneVisitForTodayAndChecksInWithThatLocker()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 5, SingleVisitSale());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.LockerId.ShouldBe(TestLockers.IdOf(5));
        var visit = (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
        attendance.SubscriptionId.ShouldBe(visit.Id);
        visit.IsSingleSession.ShouldBeTrue();
        visit.StartDate.ShouldBe(Today());
        visit.UsedSessions.ShouldBe(1);
        visit.Price.ShouldBe(SingleVisitPrice);
    }

    [Fact]
    public async Task CheckIn_SingleVisitSaleButTheLockerIsTaken_Returns409AndSellsNothing()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var holder = await AddMemberAsync();
        using (var first = await CheckInAsync(client, token, holder.Id, lockerNumber: 9, SingleVisitSale()))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var member = await AddMemberAsync();

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 9, SingleVisitSale());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await StoredSubscriptionsAsync(member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckIn_SingleVisitSaleWithNumbers_Returns400SaleInvalid()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(
            client, token, member.Id, lockerNumber: 1, new { kind = "SingleVisit", durationDays = 30, sessionCount = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "sale")).ShouldBe("Attendance.SaleInvalid");
        (await StoredSubscriptionsAsync(member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckIn_SingleVisitSaleWhileTheMemberIsInside_Returns422AlreadyCheckedInAndSellsNothing()
    {
        var (client, token) = await StaffClientAsync();
        await SetPricesAsync();
        var member = await AddMemberAsync();
        using (var first = await CheckInAsync(client, token, member.Id, lockerNumber: 2, SingleVisitSale()))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var response = await CheckInAsync(client, token, member.Id, lockerNumber: 4, SingleVisitSale());

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.AlreadyCheckedIn");
        (await StoredSubscriptionsAsync(member.Id)).ShouldHaveSingleItem();
    }

    // ---- Helpers ----

    private static object PlanSale(int days, int sessions) =>
        new { kind = "Membership", durationDays = days, sessionCount = sessions };

    private static object SingleVisitSale() => new { kind = "SingleVisit" };

    private Task SetPricesAsync() =>
        TestPlans.SetPricesAsync(Fixture, sessionPrice: SessionPrice, singleVisitPrice: SingleVisitPrice);

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync(bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");
        if (!active)
        {
            member.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A row written directly, so states that take real days to reach can be set up in one step.</summary>
    private async Task InsertSubscriptionAsync(
        Guid memberId, DateOnly start, DateOnly end, DateOnly? frozenSince = null, int usedSessions = 0)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 900000, 30, 12,
                    {start}, {end}, {usedSessions}, {frozenSince}, 0, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task<List<Subscription>> StoredSubscriptionsAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> CheckInAsync(
        HttpClient client, string token, Guid memberId, int lockerNumber, object sale) =>
        SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(lockerNumber), sale });

    private static async Task<AttendanceResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}

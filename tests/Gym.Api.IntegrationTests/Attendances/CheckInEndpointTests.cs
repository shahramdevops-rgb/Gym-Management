using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common;
using Gym.Application.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// <c>POST /api/members/{memberId}/attendance/check-in</c> (BUSINESS_RULES.md §7). Front desk
/// work, so both roles.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CheckInEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Happy path ----

    [Fact]
    public async Task CheckIn_ActiveSubscriptionAndFreeLockers_AssignsAFreeLockerAndConsumesASession()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CreateLockerAsync(ownerClient, ownerToken, 2);
        await CreateLockerAsync(ownerClient, ownerToken, 1);

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.MemberId.ShouldBe(member.Id);
        // Which locker is not specified: the pick is random (BUSINESS_RULES.md §7), so the
        // assertion is that it is one of the free ones, not which one.
        attendance.LockerNumber.ShouldBeOneOf(1, 2);
        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_RepeatedVisits_DoesNotAlwaysPickTheSameLocker()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        for (var number = 1; number <= 5; number++)
        {
            await CreateLockerAsync(ownerClient, ownerToken, number);
        }

        // Check out after each visit, so every draw sees all five lockers free. Ten visits stays
        // inside the plan's twelve sessions.
        var assigned = new List<int?>();
        for (var visit = 0; visit < 10; visit++)
        {
            var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
            assigned.Add(attendance.LockerNumber);
            await CheckOutOkAsync(staffClient, staffToken, attendance.Id);
        }

        // Statistical, but not flaky: if the pick really is random, the odds of ten draws from
        // five lockers all landing on the same one are 5^-9. Ordering by number again — the
        // behaviour this replaced — fails this every single run.
        assigned.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task CheckIn_AsOwner_Succeeds()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(ownerClient, ownerToken, member.Id, plan.Id);

        using var response = await CheckInAsync(ownerClient, ownerToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CheckIn_NoFreeLocker_Returns201WithNullLocker()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.LockerId.ShouldBeNull();
        attendance.LockerNumber.ShouldBeNull();
    }

    // ---- Subscription preconditions ----

    [Fact]
    public async Task CheckIn_ExpiredSubscription_Returns422SubscriptionsExpired()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-40), today.AddDays(-10));

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Expired");
    }

    [Fact]
    public async Task CheckIn_FrozenSubscription_Returns422SubscriptionsFrozen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), frozenSince: today.AddDays(-1));

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Frozen");
    }

    [Fact]
    public async Task CheckIn_ExhaustedSubscription_Returns422SubscriptionsNoSessionsLeft()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), usedSessions: 12);

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NoSessionsLeft");
    }

    [Fact]
    public async Task CheckIn_ActiveAndQueuedSubscriptions_ConsumesFromTheActiveOne()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        // A member who renewed early: the queued one ends later, so ordering by end date would
        // pick it and refuse the member for the rest of the term they already paid for.
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), usedSessions: 3);
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(25), today.AddDays(54));

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var subscriptions = await StoredSubscriptionsAsync(member.Id);
        subscriptions.Single(s => s.StartDate == today.AddDays(-5)).UsedSessions.ShouldBe(4);
        // The queued one is untouched: neither used nor moved.
        var queued = subscriptions.Single(s => s.StartDate == today.AddDays(25));
        queued.UsedSessions.ShouldBe(0);
        queued.EndDate.ShouldBe(today.AddDays(54));
    }

    [Fact]
    public async Task CheckIn_ExhaustedWithAQueuedRenewal_PromotesTheQueuedOneAndLetsTheMemberIn()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), usedSessions: 12);
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(25), today.AddDays(54));

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var subscriptions = await StoredSubscriptionsAsync(member.Id);
        var exhausted = subscriptions.Single(s => s.StartDate == today.AddDays(-5));
        exhausted.EndDate.ShouldBe(today.AddDays(-1));
        exhausted.UsedSessions.ShouldBe(12);
        // The renewal moved to today and kept its 30 days, and this visit came out of it.
        var promoted = subscriptions.Single(s => s.StartDate == today);
        promoted.EndDate.ShouldBe(today.AddDays(29));
        promoted.UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_ExhaustedOnItsFirstDayWithAQueuedRenewal_Returns422SubscriptionsNextStartsTomorrow()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        // Every session used on the day the plan was bought, then renewed the same day: the
        // exhausted one was closed to today, so the renewal starts tomorrow (BUSINESS_RULES.md
        // §4) and the member cannot come in again today.
        await InsertSubscriptionAsync(member.Id, plan.Id, today, today, usedSessions: 12);
        await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(1), today.AddDays(30));

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        // Not "Subscriptions.NotStarted": the renewal is the row that refuses the visit, but the
        // reason the member needs is that today is over for them, not that a sale went wrong.
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NextStartsTomorrow");
        // Nothing moved: the refused visit consumed no session and left both rows where they were.
        var subscriptions = await StoredSubscriptionsAsync(member.Id);
        subscriptions.Single(s => s.StartDate == today).EndDate.ShouldBe(today);
        var queued = subscriptions.Single(s => s.StartDate == today.AddDays(1));
        queued.UsedSessions.ShouldBe(0);
        queued.EndDate.ShouldBe(today.AddDays(30));
    }

    [Fact]
    public async Task CheckIn_NoSubscriptionAtAll_Returns422AttendanceNoSubscription()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NoSubscription");
    }

    // ---- Member and attendance preconditions ----

    [Fact]
    public async Task CheckIn_InactiveMember_Returns422MembersInactive()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync(active: false);

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
    }

    [Fact]
    public async Task CheckIn_AlreadyCheckedIn_Returns422AttendanceAlreadyCheckedIn()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await CheckInAsync(staffClient, staffToken, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.AlreadyCheckedIn");
    }

    [Fact]
    public async Task CheckIn_UnknownMember_Returns404()
    {
        var (staffClient, staffToken) = await StaffClientAsync();

        using var response = await CheckInAsync(staffClient, staffToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task CheckIn_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync(
            $"/api/members/{Guid.CreateVersion7()}/attendance/check-in", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Locker occupancy now that check-in exists ----

    [Fact]
    public async Task CheckIn_ThenGetLocker_ShowsOccupiedTrue()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);

        await CheckInOkAsync(staffClient, staffToken, member.Id);

        var fetched = await GetLockerOkAsync(ownerClient, ownerToken, locker.Id);
        fetched.IsOccupied.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckIn_ThenSetLockerOutOfService_Returns422LockersOccupied()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);
        await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await SendAsync(ownerClient, ownerToken, HttpMethod.Post, $"/api/lockers/{locker.Id}/out-of-service");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.Occupied");
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

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync(bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98912{suffix:D7}");
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

    private async Task<Plan> AddPlanAsync()
    {
        var plan = Plan.Create("پلن", 30, 12, 900_000m).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    /// <summary>A row written directly, so states that take real days to reach can be set up in one step.</summary>
    private async Task InsertSubscriptionAsync(
        Guid memberId, Guid planId, DateOnly start, DateOnly end, DateOnly? frozenSince = null, int usedSessions = 0)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, {planId}, 900000, 30, 12,
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

    private async Task<Subscription> StoredSubscriptionAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.MemberId == memberId, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, Guid planId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });

    private static async Task AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await AssignAsync(client, token, memberId, planId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<LockerResponse> CreateLockerAsync(HttpClient client, string token, int number)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, "/api/lockers", new { number });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<LockerResponse> GetLockerOkAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/lockers/{id}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");

    private static async Task<AttendanceResponse> CheckInOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await CheckInAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static async Task CheckOutOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");
        response.EnsureSuccessStatusCode();
    }

    private static async Task<AttendanceResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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

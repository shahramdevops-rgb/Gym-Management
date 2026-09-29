using System.Net;
using System.Net.Http.Json;

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
/// A frozen member who comes in is unfrozen (BUSINESS_RULES.md §4 <i>Freeze</i>, roadmap 6.5.9):
/// check-in ends the freeze by the ordinary unfreeze rules and uses a session, in one transaction.
/// Every test checks in as Staff, who cannot unfreeze by hand.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CheckInUnfreezeEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    /// <summary><c>Gym:MaxFreezeDaysPerSubscription</c> in the test configuration.</summary>
    private const int MaxFreezeDays = 30;

    private static int _phoneSuffix;

    [Fact]
    public async Task CheckIn_FrozenPlan_UnfreezesItExtendsItsEndAndUsesASession()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        var id = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.SubscriptionId.ShouldBe(id);
        attendance.UnfrozenDays.ShouldBe(4);
        var stored = await StoredAsync(id);
        stored.FrozenSince.ShouldBeNull();
        stored.TotalFrozenDays.ShouldBe(4);
        stored.EndDate.ShouldBe(today.AddDays(23));
        stored.UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_FrozenPlanWithAQueuedOne_ShiftsTheQueuedPlanByTheSameDays()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));
        var queuedId = await InsertMembershipAsync(member.Id, today.AddDays(20), today.AddDays(49));

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var queued = await StoredAsync(queuedId);
        queued.StartDate.ShouldBe(today.AddDays(24));
        queued.EndDate.ShouldBe(today.AddDays(53));
        queued.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CheckIn_FrozenLongerThanTheAllowanceLeft_ExtendsOnlyByTheDaysLeft()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        // 27 of 30 days used by earlier freezes; frozen again 10 days ago, so only 3 are left.
        var id = await InsertMembershipAsync(
            member.Id, today.AddDays(-12), today.AddDays(17), frozenSince: today.AddDays(-10), totalFrozenDays: MaxFreezeDays - 3);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response)).UnfrozenDays.ShouldBe(3);
        var stored = await StoredAsync(id);
        stored.EndDate.ShouldBe(today.AddDays(20));
        stored.TotalFrozenDays.ShouldBe(MaxFreezeDays);
    }

    [Fact]
    public async Task CheckIn_FrozenPlanThatRanOutWhileFrozen_Returns422ExpiredAndStaysFrozen()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        // Frozen 40 days ago with 9 days left; the allowance adds back only 30, so it ended yesterday.
        var id = await InsertMembershipAsync(member.Id, today.AddDays(-60), today.AddDays(-31), frozenSince: today.AddDays(-40));

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Expired");
        // Nothing saved: the Owner decides what to do with it.
        var stored = await StoredAsync(id);
        stored.FrozenSince.ShouldBe(today.AddDays(-40));
        stored.EndDate.ShouldBe(today.AddDays(-31));
        stored.TotalFrozenDays.ShouldBe(0);
        stored.UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CheckIn_SingleVisitHeldForToday_UsesItAndLeavesTheFreezeAlone()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        var frozenId = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));
        var visitId = await InsertSingleVisitAsync(member.Id, today);

        using var response = await TestLockers.CheckInAsync(client, token, member.Id, lockerNumber: 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.SubscriptionId.ShouldBe(visitId);
        attendance.UnfrozenDays.ShouldBeNull();
        (await StoredAsync(frozenId)).FrozenSince.ShouldBe(today.AddDays(-4));
    }

    [Fact]
    public async Task CheckIn_SingleVisitSoldWithTheCheckIn_UsesItAndLeavesTheFreezeAlone()
    {
        var (client, token) = await StaffClientAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 75_000m, singleVisitPrice: 120_000m);
        var member = await AddMemberAsync();
        var today = Today();
        var frozenId = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(3), sale = new { kind = "SingleVisit" } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attendance = await ReadAsync(response);
        attendance.SubscriptionId.ShouldNotBe(frozenId);
        attendance.UnfrozenDays.ShouldBeNull();
        (await StoredAsync(frozenId)).FrozenSince.ShouldBe(today.AddDays(-4));
    }

    [Fact]
    public async Task CheckIn_PlanSoldWithTheCheckInBehindAFrozenPlan_Returns422AndLeavesTheFreezeAlone()
    {
        var (client, token) = await StaffClientAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 75_000m, singleVisitPrice: 120_000m);
        var member = await AddMemberAsync();
        var today = Today();
        var frozenId = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in",
            new { lockerId = TestLockers.IdOf(3), sale = new { kind = "Membership", sessionCount = 12 } });

        // A sale is made to be used: the new plan would queue behind the frozen one, so the check-in
        // is refused and rolled back rather than unfreezing the old plan and keeping the new one.
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var stored = await StoredForMemberAsync(member.Id);
        stored.ShouldHaveSingleItem().FrozenSince.ShouldBe(today.AddDays(-4));
    }

    [Fact]
    public async Task CheckIn_PlanNotFrozen_SaysNothingWasUnfrozen()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19));

        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 3);

        attendance.UnfrozenDays.ShouldBeNull();
    }

    [Fact]
    public async Task CancelCheckIn_AfterAnUnfreeze_GivesTheSessionBackAndLeavesThePlanUnfrozen()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();
        var id = await InsertMembershipAsync(member.Id, today.AddDays(-10), today.AddDays(19), frozenSince: today.AddDays(-4));
        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 3);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendance.Id}/cancel", CancelCheckInBody.KeepPurchases);

        response.EnsureSuccessStatusCode();
        var stored = await StoredAsync(id);
        stored.UsedSessions.ShouldBe(0);
        // BUSINESS_RULES.md §7 Cancel check-in: the Owner freezes it again if it was a mistake.
        stored.FrozenSince.ShouldBeNull();
        stored.EndDate.ShouldBe(today.AddDays(23));
    }

    // ---- Helpers ----

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

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("مریم رضایی", $"+98913{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A 12-session plan written directly, so a freeze that began days ago takes one step.</summary>
    private async Task<Guid> InsertMembershipAsync(
        Guid memberId, DateOnly start, DateOnly end, DateOnly? frozenSince = null, int totalFrozenDays = 0)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 900000, 30, 10,
                    {start}, {end}, 0, {frozenSince}, {totalFrozenDays}, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<Guid> InsertSingleVisitAsync(Guid memberId, DateOnly day)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions, is_single_session,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 120000, 1, 1, true,
                    {day}, {day}, 0, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<Subscription> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<Subscription>> StoredForMemberAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .ToListAsync(TestContext.Current.CancellationToken);
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

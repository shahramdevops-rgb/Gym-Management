using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Lockers.ListLockerVisitsToday;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Lockers;

/// <summary>
/// <c>GET /api/lockers/{id}/today</c>: BUSINESS_RULES.md §6 <i>Who had a locker today</i> (roadmap
/// 6.5.10). Today only, oldest first, a visit counted for the locker it holds now, and cancelled
/// check-ins listed. Signed in as Staff, because the map is the desk's screen.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LockerVisitsTodayEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task ListLockerVisitsToday_TwoVisitsOnTheLocker_ListsBothOldestFirstWithNames()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await AddMemberWithPlanAsync(client, token, "رضا احمدی");
        var sara = await AddMemberWithPlanAsync(client, token, "سارا کریمی");

        var first = await TestLockers.CheckInOkAsync(client, token, reza.Id, lockerNumber: 5);
        (await PostAsync(client, token, $"/api/attendance/{first.Id}/check-out")).Dispose();
        var second = await TestLockers.CheckInOkAsync(client, token, sara.Id, lockerNumber: 5);

        var visits = await TodayOkAsync(client, token, lockerNumber: 5);

        visits.Select(visit => visit.AttendanceId).ShouldBe([first.Id, second.Id]);
        visits[0].MemberId.ShouldBe(reza.Id);
        visits[0].MemberFullName.ShouldBe("رضا احمدی");
        visits[0].CheckedOutAt.ShouldNotBeNull();
        visits[1].MemberFullName.ShouldBe("سارا کریمی");
        visits[1].CheckedOutAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListLockerVisitsToday_VisitBeforeTodaysMidnight_IsLeftOut()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await AddMemberWithPlanAsync(client, token, "رضا احمدی");
        // A minute before the gym's midnight: yesterday, however close.
        await InsertClosedVisitAsync(reza.Id, lockerNumber: 5, checkedInAt: StartOfTodayUtc().AddMinutes(-1));

        var visits = await TodayOkAsync(client, token, lockerNumber: 5);

        visits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListLockerVisitsToday_VisitOnAnotherLocker_IsLeftOut()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await AddMemberWithPlanAsync(client, token, "رضا احمدی");
        await TestLockers.CheckInOkAsync(client, token, reza.Id, lockerNumber: 6);

        var visits = await TodayOkAsync(client, token, lockerNumber: 5);

        visits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListLockerVisitsToday_CancelledCheckIn_IsListedWithCancelledAt()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await AddMemberWithPlanAsync(client, token, "رضا احمدی");
        var visit = await TestLockers.CheckInOkAsync(client, token, reza.Id, lockerNumber: 5);
        using (var cancelled = await PostAsync(client, token, $"/api/attendance/{visit.Id}/cancel", CancelCheckInBody.KeepPurchases))
        {
            cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var visits = await TodayOkAsync(client, token, lockerNumber: 5);

        visits.ShouldHaveSingleItem().CancelledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListLockerVisitsToday_VisitMovedAway_IsListedUnderTheNewLockerOnly()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await AddMemberWithPlanAsync(client, token, "رضا احمدی");
        var visit = await TestLockers.CheckInOkAsync(client, token, reza.Id, lockerNumber: 5);
        using (var moved = await PostAsync(client, token, $"/api/attendance/{visit.Id}/move-locker", new { lockerId = TestLockers.IdOf(7) }))
        {
            moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await TodayOkAsync(client, token, lockerNumber: 5)).ShouldBeEmpty();
        (await TodayOkAsync(client, token, lockerNumber: 7)).ShouldHaveSingleItem().AttendanceId.ShouldBe(visit.Id);
    }

    [Fact]
    public async Task ListLockerVisitsToday_UnknownLocker_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await TodayAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListLockerVisitsToday_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync($"/api/lockers/{TestLockers.IdOf(5)}/today", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Member> AddMemberWithPlanAsync(HttpClient client, string token, string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98917{suffix:D7}");
        var plan = await TestPlans.AddAsync(Fixture);
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var assigned = await PostAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        return member;
    }

    private DateTimeOffset StartOfTodayUtc()
    {
        using var scope = Fixture.CreateScope();
        var calendar = scope.ServiceProvider.GetRequiredService<IGymCalendar>();

        return calendar.StartOfDayUtc(calendar.Today());
    }

    /// <summary>
    /// A closed visit written directly, so it can start at a moment the API would never give it (the
    /// API stamps check-in with the real clock). It uses the member's own subscription.
    /// </summary>
    private async Task InsertClosedVisitAsync(Guid memberId, int lockerNumber, DateTimeOffset checkedInAt)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriptionId = await db.Subscriptions
            .Where(s => s.MemberId == memberId)
            .Select(s => s.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, checked_out_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, {subscriptionId}, {TestLockers.IdOf(lockerNumber)}, {checkedInAt}, {checkedInAt.AddMinutes(30)}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> TodayAsync(HttpClient client, string token, Guid lockerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/lockers/{lockerId}/today");

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<List<LockerVisitResponse>> TodayOkAsync(HttpClient client, string token, int lockerNumber)
    {
        using var response = await TodayAsync(client, token, TestLockers.IdOf(lockerNumber));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<LockerVisitResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}

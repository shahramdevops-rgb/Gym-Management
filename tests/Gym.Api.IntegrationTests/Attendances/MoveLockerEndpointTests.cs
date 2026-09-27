using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
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
/// <c>POST /api/attendance/{id}/move-locker</c> (BUSINESS_RULES.md §7 <i>Moving to another
/// locker</i>): the desk gave the wrong locker, or it broke while in use.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MoveLockerEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task MoveLocker_ToAFreeLocker_MovesTheVisitAndFreesTheOldLocker()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var moved = await ReadAsync(response);
        moved.Id.ShouldBe(member.VisitId);
        moved.LockerId.ShouldBe(TestLockers.IdOf(40));
        moved.LockerNumber.ShouldBe(40);
        moved.CheckedOutAt.ShouldBeNull();
        (await LockerAsync(client, token, 12)).IsOccupied.ShouldBeFalse();
        (await LockerAsync(client, token, 40)).OccupiedByMemberId.ShouldBe(member.Id);
    }

    [Fact]
    public async Task MoveLocker_AnyMove_ConsumesNoSessionAndKeepsTheVisitsCharges()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);
        using (var charged = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{member.VisitId}/service-charges",
                   new { kind = "Cardio", amount = 50_000m }))
        {
            charged.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 13);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).ServiceCharges.ShouldHaveSingleItem().Amount.ShouldBe(50_000m);
        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task MoveLocker_FromAReservePlace_TakesTheLockerAndGivesUpThePlace()
    {
        var (client, token) = await StaffClientAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);
        var member = await AddMemberWithPlanAsync(client, token);
        using var placed = await TestLockers.CheckInOnReservePlaceAsync(client, token, member.Id);
        var visit = await ReadAsync(placed);
        // A locker is repaired: the member gets it as soon as it is free.
        (await SendAsync(client, token, HttpMethod.Post, $"/api/lockers/{TestLockers.IdOf(5)}/in-service")).Dispose();

        using var response = await MoveAsync(client, token, visit.Id, lockerNumber: 5);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var moved = await ReadAsync(response);
        moved.UsesReservePlace.ShouldBeFalse();
        moved.LockerNumber.ShouldBe(5);
        (await StoredReserveSlotAsync(visit.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task MoveLocker_TargetSomeoneHolds_Returns409LockerTaken()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);
        await MemberInsideAsync(client, token, lockerNumber: 40);

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockerTaken");
        (await LockerAsync(client, token, 12)).OccupiedByMemberId.ShouldBe(member.Id);
    }

    [Fact]
    public async Task MoveLocker_TargetOutOfService_Returns422LockersOutOfService()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);
        (await SendAsync(client, token, HttpMethod.Post, $"/api/lockers/{TestLockers.IdOf(40)}/out-of-service")).Dispose();

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.OutOfService");
    }

    [Fact]
    public async Task MoveLocker_ToTheLockerItHolds_Returns422SameLocker()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 12);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.SameLocker");
    }

    [Fact]
    public async Task MoveLocker_ClosedVisit_Returns422NotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);
        (await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{member.VisitId}/check-out")).Dispose();

        using var response = await MoveAsync(client, token, member.VisitId, lockerNumber: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotOpen");
    }

    [Fact]
    public async Task MoveLocker_UnknownVisit_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await MoveAsync(client, token, Guid.CreateVersion7(), lockerNumber: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotFound");
    }

    [Fact]
    public async Task MoveLocker_UnknownLocker_Returns404LockersNotFound()
    {
        var (client, token) = await StaffClientAsync();
        var member = await MemberInsideAsync(client, token, lockerNumber: 12);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{member.VisitId}/move-locker", new { lockerId = Guid.CreateVersion7() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.NotFound");
    }

    [Fact]
    public async Task MoveLocker_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendance/{Guid.CreateVersion7()}/move-locker", new { lockerId = TestLockers.IdOf(1) }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Member> AddMemberWithPlanAsync(HttpClient client, string token)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98914{suffix:D7}");
        // A plan per member, named apart: plan names are unique.
        var plan = Plan.Create($"پلن {suffix}", 30, 12, 900_000m).Value;

        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            db.Plans.Add(plan);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var assigned = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions", new { planId = plan.Id });
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        return member;
    }

    private async Task<(Guid Id, Guid VisitId)> MemberInsideAsync(HttpClient client, string token, int lockerNumber)
    {
        var member = await AddMemberWithPlanAsync(client, token);
        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber);

        return (member.Id, visit.Id);
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, string token, Guid attendanceId, int lockerNumber) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/move-locker", new { lockerId = TestLockers.IdOf(lockerNumber) });

    private static async Task<LockerResponse> LockerAsync(HttpClient client, string token, int number)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/lockers/{TestLockers.IdOf(number)}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<Subscription> StoredSubscriptionAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.MemberId == memberId, TestContext.Current.CancellationToken);
    }

    private async Task<int?> StoredReserveSlotAsync(Guid attendanceId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .Where(a => a.Id == attendanceId)
            .Select(a => a.ReserveSlot)
            .SingleAsync(TestContext.Current.CancellationToken);
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

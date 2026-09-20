using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Common;
using Gym.Application.Lockers;
using Gym.Domain.Attendances;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// The nightly auto-checkout job's own logic (BUSINESS_RULES.md §7). No HTTP endpoint calls
/// this — Gym.Infrastructure/Jobs schedules it with Hangfire — so these tests resolve
/// <see cref="AutoCheckoutHandler"/> from a DI scope directly, the same way the job itself will
/// be invoked.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AutoCheckoutHandlerTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task Handle_OpenAttendanceWithLocker_ClosesItAndFreesTheLockerWithoutRestoringTheSession()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        var closed = await RunJobAsync();

        closed.ShouldBe(1);
        var stored = await StoredAttendanceAsync(attendance.Id);
        stored.CheckedOutAt.ShouldNotBeNull();
        stored.AutoClosedAt.ShouldNotBeNull();
        stored.AutoClosedAt.ShouldBe(stored.CheckedOutAt);
        stored.CancelledAt.ShouldBeNull();
        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(1);

        var fetchedLocker = await GetLockerOkAsync(ownerClient, ownerToken, locker.Id);
        fetchedLocker.IsOccupied.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_AlreadyCheckedOutAttendance_LeavesItUnchanged()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        await CheckOutOkAsync(staffClient, staffToken, attendance.Id);
        var before = await StoredAttendanceAsync(attendance.Id);

        var closed = await RunJobAsync();

        closed.ShouldBe(0);
        var stored = await StoredAttendanceAsync(attendance.Id);
        stored.CheckedOutAt.ShouldBe(before.CheckedOutAt);
        stored.AutoClosedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CancelledAttendance_LeavesItUnchanged()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        await CancelOkAsync(staffClient, staffToken, attendance.Id);
        var before = await StoredAttendanceAsync(attendance.Id);

        var closed = await RunJobAsync();

        closed.ShouldBe(0);
        var stored = await StoredAttendanceAsync(attendance.Id);
        stored.CancelledAt.ShouldBe(before.CancelledAt);
        stored.CheckedOutAt.ShouldBe(before.CheckedOutAt);
        stored.AutoClosedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_SeveralOpenAttendances_ClosesAllOfThem()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var plan = await AddPlanAsync();
        var attendanceIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var member = await AddMemberAsync();
            await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
            attendanceIds.Add((await CheckInOkAsync(staffClient, staffToken, member.Id)).Id);
        }

        var closed = await RunJobAsync();

        closed.ShouldBe(3);
        foreach (var id in attendanceIds)
        {
            var stored = await StoredAttendanceAsync(id);
            stored.CheckedOutAt.ShouldNotBeNull();
            stored.AutoClosedAt.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Handle_NoOpenAttendances_ReturnsZero()
    {
        var closed = await RunJobAsync();

        closed.ShouldBe(0);
    }

    // ---- Helpers ----

    private async Task<int> RunJobAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>()
            .Handle(TestContext.Current.CancellationToken);
    }

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

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Member.Create("رضا احمدی", $"+98914{suffix:D7}", null).Value;

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

    private async Task<Subscription> StoredSubscriptionAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.MemberId == memberId, TestContext.Current.CancellationToken);
    }

    private async Task<Attendance> StoredAttendanceAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
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

    private static async Task<AttendanceResponse> CheckOutOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private static async Task<AttendanceResponse> CancelOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/cancel");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
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

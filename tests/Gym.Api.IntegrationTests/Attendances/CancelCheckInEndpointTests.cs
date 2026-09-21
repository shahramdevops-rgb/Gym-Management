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

using Npgsql;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary><c>POST /api/attendance/{id}/cancel</c> (BUSINESS_RULES.md §7). Front desk work, so both roles.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CancelCheckInEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int CancelWindowMinutes = 30; // Gym:CancelCheckInWindowMinutes (BUSINESS_RULES.md §0).

    private static int _phoneSuffix;

    [Fact]
    public async Task Cancel_WithinWindow_RestoresSessionAndFreesTheLocker()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(1);

        using var response = await CancelAsync(staffClient, staffToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await ReadAsync(response);
        cancelled.CancelledAt.ShouldNotBeNull();
        cancelled.CheckedOutAt.ShouldNotBeNull();
        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(0);

        var fetched = await GetLockerOkAsync(ownerClient, ownerToken, locker.Id);
        fetched.IsOccupied.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_PastTheWindow_Returns422AttendanceCancelWindowExpired()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var subscriptionId = await InsertSubscriptionAsync(member.Id, plan.Id, today, today.AddDays(29));
        var attendanceId = await InsertOpenAttendanceAsync(
            member.Id, subscriptionId, checkedInAt: DateTimeOffset.UtcNow.AddMinutes(-(CancelWindowMinutes + 1)));

        using var response = await CancelAsync(staffClient, staffToken, attendanceId);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.CancelWindowExpired");
    }

    [Fact]
    public async Task Cancel_AlreadyCheckedOut_Returns422AttendanceNotOpen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        await SendAsync(staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{attendance.Id}/check-out");

        using var response = await CancelAsync(staffClient, staffToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotOpen");
    }

    [Fact]
    public async Task Cancel_UnknownId_Returns404()
    {
        var (staffClient, staffToken) = await StaffClientAsync();

        using var response = await CancelAsync(staffClient, staffToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotFound");
    }

    [Fact]
    public async Task Cancel_AsOwner_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await CancelAsync(ownerClient, ownerToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cancel_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync(
            $"/api/attendance/{Guid.CreateVersion7()}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Database constraints ----

    /// <summary>
    /// CLAUDE.md: invariants are enforced by the database too. Cancelling always closes the
    /// attendance at the same moment (<c>Attendance.Cancel</c> sets both together); a row written
    /// past that, with raw SQL, proves the database refuses it independently of the app.
    /// </summary>
    [Fact]
    public async Task Insert_CancelledAtWithoutMatchingCheckedOutAt_RejectedByTheCheckConstraint()
    {
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var subscriptionId = await InsertSubscriptionAsync(member.Id, plan.Id, today, today.AddDays(29));
        var checkedInAt = DateTimeOffset.UtcNow;

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertMismatchedCancellationAsync(
            member.Id, subscriptionId, checkedInAt, checkedOutAt: checkedInAt.AddMinutes(5), cancelledAt: checkedInAt.AddMinutes(6)));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_attendances_cancellation");
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

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Member.Create("رضا احمدی", $"+98912{suffix:D7}", null).Value;

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

    /// <summary>A row written directly, so a subscription with sessions left can be set up in one step.</summary>
    private async Task<Guid> InsertSubscriptionAsync(Guid memberId, Guid planId, DateOnly start, DateOnly end)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, {planId}, 900000, 30, 12, {start}, {end}, 1, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task InsertMismatchedCancellationAsync(
        Guid memberId, Guid subscriptionId, DateTimeOffset checkedInAt, DateTimeOffset checkedOutAt, DateTimeOffset cancelledAt)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, {subscriptionId}, NULL, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    /// <summary>A row written directly, so a check-in older than the cancel window can be set up in one step.</summary>
    private async Task<Guid> InsertOpenAttendanceAsync(Guid memberId, Guid subscriptionId, DateTimeOffset checkedInAt)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, created_at)
            VALUES ({id}, {memberId}, {subscriptionId}, NULL, {checkedInAt}, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
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

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string token, Guid attendanceId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/cancel");

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

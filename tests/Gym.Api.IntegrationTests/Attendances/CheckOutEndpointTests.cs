using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary><c>POST /api/attendance/{id}/check-out</c> (BUSINESS_RULES.md §7). Front desk work, so both roles.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CheckOutEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task CheckOut_OpenAttendanceWithLocker_ClosesItAndFreesTheLocker()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await CheckOutAsync(staffClient, staffToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var checkedOut = await ReadAsync(response);
        checkedOut.CheckedOutAt.ShouldNotBeNull();
        checkedOut.CancelledAt.ShouldBeNull();

        var fetched = await GetLockerOkAsync(ownerClient, ownerToken, locker.Id);
        fetched.IsOccupied.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckOut_AsOwner_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await CheckOutAsync(ownerClient, ownerToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CheckOut_AlreadyCheckedOut_Returns422AttendanceNotOpen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        await CheckOutAsync(staffClient, staffToken, attendance.Id);

        using var response = await CheckOutAsync(staffClient, staffToken, attendance.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotOpen");
    }

    [Fact]
    public async Task CheckOut_UnknownId_Returns404()
    {
        var (staffClient, staffToken) = await StaffClientAsync();

        using var response = await CheckOutAsync(staffClient, staffToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotFound");
    }

    [Fact]
    public async Task CheckOut_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync(
            $"/api/attendance/{Guid.CreateVersion7()}/check-out", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98912{suffix:D7}");

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

    private static Task<HttpResponseMessage> CheckOutAsync(HttpClient client, string token, Guid attendanceId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");

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

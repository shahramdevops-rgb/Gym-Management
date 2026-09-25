using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary><c>GET /api/attendance/currently-inside</c> (BUSINESS_RULES.md §7). Front desk board.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CurrentlyInsideEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task CurrentlyInside_OpenAttendance_ShowsMemberNameAndLockerNumber()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync("سارا محمدی");
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 3);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await SendAsync(staffClient, staffToken, HttpMethod.Get, "/api/attendance/currently-inside");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadPageAsync(response);
        var row = page.Items.ShouldHaveSingleItem();
        row.AttendanceId.ShouldBe(attendance.Id);
        row.MemberId.ShouldBe(member.Id);
        row.MemberFullName.ShouldBe("سارا محمدی");
        row.LockerNumber.ShouldBe(locker.Number);
    }

    [Fact]
    public async Task CurrentlyInside_LimitedSubscription_ShowsSessionsUsedTotalAndEndDate()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync("سارا محمدی");
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await SendAsync(staffClient, staffToken, HttpMethod.Get, "/api/attendance/currently-inside");

        var row = (await ReadPageAsync(response)).Items.ShouldHaveSingleItem();
        // The visit that is being shown is the one that consumed the session, so one is used.
        row.SubscriptionId.ShouldBe(attendance.SubscriptionId);
        row.TotalSessions.ShouldBe(12);
        row.UsedSessions.ShouldBe(1);
        row.RemainingSessions.ShouldBe(11);
        row.SubscriptionEndDate.ShouldBe(await SubscriptionEndDateAsync(attendance.SubscriptionId));
    }

    [Fact]
    public async Task CurrentlyInside_UnlimitedSubscription_LeavesSessionCountsNull()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync("رضا احمدی");
        var plan = await AddUnlimitedPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await SendAsync(staffClient, staffToken, HttpMethod.Get, "/api/attendance/currently-inside");

        var row = (await ReadPageAsync(response)).Items.ShouldHaveSingleItem();
        // Unlimited: nothing to count against, so the board says so instead of drawing a bar.
        row.TotalSessions.ShouldBeNull();
        row.RemainingSessions.ShouldBeNull();
    }

    [Fact]
    public async Task CurrentlyInside_CheckedOutAndCancelledAttendances_AreExcluded()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var plan = await AddPlanAsync();

        var checkedOutMember = await AddMemberAsync("علی رضایی");
        await AssignOkAsync(staffClient, staffToken, checkedOutMember.Id, plan.Id);
        var checkedOutAttendance = await CheckInOkAsync(staffClient, staffToken, checkedOutMember.Id);
        await SendAsync(staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{checkedOutAttendance.Id}/check-out");

        var cancelledMember = await AddMemberAsync("مریم کریمی");
        await AssignOkAsync(staffClient, staffToken, cancelledMember.Id, plan.Id);
        var cancelledAttendance = await CheckInOkAsync(staffClient, staffToken, cancelledMember.Id);
        await SendAsync(staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{cancelledAttendance.Id}/cancel");

        var stillInsideMember = await AddMemberAsync("حسین قاسمی");
        await AssignOkAsync(staffClient, staffToken, stillInsideMember.Id, plan.Id);
        await CheckInOkAsync(staffClient, staffToken, stillInsideMember.Id);

        using var response = await SendAsync(staffClient, staffToken, HttpMethod.Get, "/api/attendance/currently-inside");

        var page = await ReadPageAsync(response);
        var row = page.Items.ShouldHaveSingleItem();
        row.MemberId.ShouldBe(stillInsideMember.Id);
    }

    [Fact]
    public async Task CurrentlyInside_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync("/api/attendance/currently-inside", TestContext.Current.CancellationToken);

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

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98912{suffix:D7}");

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

    private async Task<Plan> AddUnlimitedPlanAsync()
    {
        var plan = Plan.Create("پلن نامحدود", 30, null, 1_500_000m).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private async Task<DateOnly> SubscriptionEndDateAsync(Guid subscriptionId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(s => s.Id == subscriptionId)
            .Select(s => s.EndDate)
            .SingleAsync(TestContext.Current.CancellationToken);
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

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");

    private static async Task<AttendanceResponse> CheckInOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await CheckInAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<CurrentlyInsideResponse>> ReadPageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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

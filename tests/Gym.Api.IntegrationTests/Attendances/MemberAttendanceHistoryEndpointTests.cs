using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// <c>GET /api/members/{memberId}/attendance</c> (roadmap 5.3). Cancelled visits are included,
/// marked by <c>CancelledAt</c> — this is a per-member operational view, not the Phase 9 reports.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MemberAttendanceHistoryEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task History_WithinDateRange_ReturnsTheEntry()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var subscriptionId = await InsertSubscriptionAsync(member.Id, plan.Id, today, today.AddDays(29));
        await InsertAttendanceAsync(member.Id, subscriptionId, checkedInAt: DateTimeOffset.UtcNow);

        using var response = await HistoryAsync(staffClient, staffToken, member.Id, from: today, to: today);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadPageAsync(response);
        page.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task History_OutsideDateRange_ExcludesTheEntry()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var subscriptionId = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-60), today.AddDays(-31));
        await InsertAttendanceAsync(member.Id, subscriptionId, checkedInAt: DateTimeOffset.UtcNow.AddDays(-45));

        using var response = await HistoryAsync(staffClient, staffToken, member.Id, from: today, to: today);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadPageAsync(response);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task History_CancelledCheckIn_IsIncludedWithCancelledAtSet()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendance = await CheckInOkAsync(staffClient, staffToken, member.Id);
        await SendAsync(staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{attendance.Id}/cancel");

        using var response = await HistoryAsync(staffClient, staffToken, member.Id);

        var page = await ReadPageAsync(response);
        var row = page.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(attendance.Id);
        row.CancelledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task History_PageSizeSmallerThanTotal_Pages()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var subscriptionId = await InsertSubscriptionAsync(member.Id, plan.Id, today, today.AddDays(29));
        var now = DateTimeOffset.UtcNow;
        await InsertAttendanceAsync(member.Id, subscriptionId, now.AddHours(-3));
        await InsertAttendanceAsync(member.Id, subscriptionId, now.AddHours(-2));
        await InsertAttendanceAsync(member.Id, subscriptionId, now.AddHours(-1));

        using var response = await HistoryAsync(staffClient, staffToken, member.Id, page: 1, pageSize: 2);

        var page = await ReadPageAsync(response);
        page.Items.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task History_FromAfterTo_Returns400InvalidDateRange()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var today = Today();

        using var response = await HistoryAsync(staffClient, staffToken, member.Id, from: today, to: today.AddDays(-1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_ToAtDateOnlyMaxValue_Returns400InsteadOfCrashing()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await HistoryAsync(staffClient, staffToken, member.Id, to: DateOnly.MaxValue);

        // 400 from ValidationFilter<T>, not an unhandled exception: the top-level code is the
        // generic "General.ValidationFailed" wrapper (docs/ARCHITECTURE.md); the field-level
        // Attendance.InvalidDateRange code lives under "errors", same shape as every other
        // FluentValidation failure in this API.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_UnknownMember_Returns404()
    {
        var (staffClient, staffToken) = await StaffClientAsync();

        using var response = await HistoryAsync(staffClient, staffToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
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

    /// <summary>A row written directly, so a subscription covering an arbitrary date range can be set up in one step.</summary>
    private async Task<Guid> InsertSubscriptionAsync(Guid memberId, Guid planId, DateOnly start, DateOnly end)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, plan_name, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, {planId}, 'پلن', 900000, 30, 12, {start}, {end}, 1, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    /// <summary>A row written directly, so a visit on an arbitrary moment can be set up in one step.</summary>
    private async Task InsertAttendanceAsync(Guid memberId, Guid subscriptionId, DateTimeOffset checkedInAt)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, checked_out_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, {subscriptionId}, NULL, {checkedInAt}, {checkedInAt.AddMinutes(30)}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, Guid planId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });

    private static async Task AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await AssignAsync(client, token, memberId, planId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");

    private static async Task<AttendanceResponse> CheckInOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await CheckInAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> HistoryAsync(
        HttpClient client, string token, Guid memberId, DateOnly? from = null, DateOnly? to = null, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (from is { } f)
        {
            query.Add($"from={f:yyyy-MM-dd}");
        }

        if (to is { } t)
        {
            query.Add($"to={t:yyyy-MM-dd}");
        }

        if (page is { } p)
        {
            query.Add($"page={p}");
        }

        if (pageSize is { } ps)
        {
            query.Add($"pageSize={ps}");
        }

        var path = $"/api/members/{memberId}/attendance";
        if (query.Count > 0)
        {
            path += "?" + string.Join('&', query);
        }

        return SendAsync(client, token, HttpMethod.Get, path);
    }

    private static async Task<PagedResponse<AttendanceResponse>> ReadPageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PagedResponse<AttendanceResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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

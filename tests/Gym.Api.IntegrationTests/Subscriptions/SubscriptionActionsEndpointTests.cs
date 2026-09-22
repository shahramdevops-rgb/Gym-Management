using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// Freeze, unfreeze and cancel: <c>POST /api/subscriptions/{id}/freeze</c>, <c>/unfreeze</c> and
/// <c>/cancel</c> (BUSINESS_RULES.md §4 Freeze, Cancel). Owner only (task 4.3).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionActionsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int MaxFreezeDays = 30; // Gym:MaxFreezeDaysPerSubscription (BUSINESS_RULES.md §0).

    private static int _phoneSuffix;

    // ---- Freeze ----

    [Fact]
    public async Task Freeze_ActiveSubscription_ByOwner_SetsFrozenSinceToday()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await FreezeAsync(ownerClient, ownerToken, sold.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var frozen = await ReadAsync(response);
        frozen.FrozenSince.ShouldBe(Today());
        frozen.Status.ShouldBe(SubscriptionStatus.Frozen);
    }

    [Fact]
    public async Task Freeze_ByStaff_Returns403()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await FreezeAsync(staffClient, staffToken, sold.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Freeze_AlreadyFrozen_Returns422SubscriptionsFrozen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await FreezeOkAsync(ownerClient, ownerToken, sold.Id);

        using var response = await FreezeAsync(ownerClient, ownerToken, sold.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Frozen");
    }

    [Fact]
    public async Task Freeze_AtTheFreezeLimit_Returns422FreezeLimitReached()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var id = await InsertSubscriptionAsync(
            member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), totalFrozenDays: MaxFreezeDays);

        using var response = await FreezeAsync(ownerClient, ownerToken, id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.FreezeLimitReached");
    }

    [Fact]
    public async Task Freeze_UnknownId_Returns404()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();

        using var response = await FreezeAsync(ownerClient, ownerToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Unfreeze ----

    [Fact]
    public async Task Unfreeze_NotFrozen_Returns422NotFrozen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await UnfreezeAsync(ownerClient, ownerToken, sold.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NotFrozen");
    }

    [Fact]
    public async Task Unfreeze_ByOwner_ExtendsEndDateByFrozenDaysAndClearsFrozenSince()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var originalEnd = today.AddDays(19);
        var id = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-10), originalEnd, frozenSince: today.AddDays(-5));

        using var response = await UnfreezeAsync(ownerClient, ownerToken, id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unfrozen = await ReadAsync(response);
        unfrozen.FrozenSince.ShouldBeNull();
        unfrozen.TotalFrozenDays.ShouldBe(5);
        unfrozen.EndDate.ShouldBe(originalEnd.AddDays(5));
    }

    [Fact]
    public async Task Unfreeze_ShiftsQueuedSubscriptions()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var originalEnd = today.AddDays(19);
        var frozenId = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-10), originalEnd, frozenSince: today.AddDays(-5));
        var queuedStart = originalEnd.AddDays(1);
        var queuedEnd = queuedStart.AddDays(29);
        var queuedId = await InsertSubscriptionAsync(member.Id, plan.Id, queuedStart, queuedEnd);

        using var response = await UnfreezeAsync(ownerClient, ownerToken, frozenId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var queued = await GetOkAsync(ownerClient, ownerToken, queuedId);
        queued.StartDate.ShouldBe(queuedStart.AddDays(5));
        queued.EndDate.ShouldBe(queuedEnd.AddDays(5));
    }

    [Fact]
    public async Task Unfreeze_FrozenLongerThanRemainingAllowance_CapsTheExtension()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var originalEnd = today.AddDays(19);

        // 27 of 30 days already used by earlier freezes; frozen again 10 days ago, so only 3 are left.
        var id = await InsertSubscriptionAsync(
            member.Id, plan.Id, today.AddDays(-10), originalEnd, frozenSince: today.AddDays(-10), totalFrozenDays: 27);

        using var response = await UnfreezeAsync(ownerClient, ownerToken, id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unfrozen = await ReadAsync(response);
        unfrozen.EndDate.ShouldBe(originalEnd.AddDays(3));
        unfrozen.TotalFrozenDays.ShouldBe(MaxFreezeDays);
    }

    [Fact]
    public async Task Unfreeze_ByStaff_Returns403()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var id = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-5), today.AddDays(24), frozenSince: today.AddDays(-2));

        using var response = await UnfreezeAsync(staffClient, staffToken, id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unfreeze_UnknownId_Returns404()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();

        using var response = await UnfreezeAsync(ownerClient, ownerToken, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancel_WithReason_ByOwner_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await CancelAsync(ownerClient, ownerToken, sold.Id, "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await ReadAsync(response);
        cancelled.CancelledAt.ShouldNotBeNull();
        cancelled.CancellationReason.ShouldBe("انصراف عضو");
        cancelled.Status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_EmptyReason_Returns400WithFieldCode()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await CancelAsync(ownerClient, ownerToken, sold.Id, "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("reason")[0].GetProperty("code").GetString()
            .ShouldBe("Subscriptions.CancelReasonRequired");
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Returns422Cancelled()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CancelOkAsync(ownerClient, ownerToken, sold.Id, "انصراف عضو");

        using var response = await CancelAsync(ownerClient, ownerToken, sold.Id, "دلیل دیگر");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Cancelled");
    }

    /// <summary>
    /// BUSINESS_RULES.md §4 Cancel (task 4.7): a service that has been consumed is not un-sold.
    /// The session is used through the real check-in endpoint, which is the only thing that
    /// consumes one.
    /// </summary>
    [Fact]
    public async Task Cancel_AfterACheckIn_Returns422AlreadyUsed()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        await CheckInOkAsync(staffClient, staffToken, member.Id);

        using var response = await CancelAsync(ownerClient, ownerToken, sold.Id, "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.AlreadyUsed");
    }

    /// <summary>
    /// The 30-minute cancel-check-in window is the intended escape hatch (BUSINESS_RULES.md §4):
    /// undoing the visit restores the session, and the subscription can be cancelled again.
    /// </summary>
    [Fact]
    public async Task Cancel_AfterTheCheckInIsCancelled_Succeeds()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
        var attendanceId = await CheckInOkAsync(staffClient, staffToken, member.Id);
        using var cancelledCheckIn = await SendAsync(
            staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{attendanceId}/cancel");
        cancelledCheckIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await CancelAsync(ownerClient, ownerToken, sold.Id, "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// An expired subscription is history, not something still to decide about
    /// (BUSINESS_RULES.md §4 Cancel, task 4.7).
    /// </summary>
    [Fact]
    public async Task Cancel_Expired_Returns422Expired()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var expiredId = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-60), today.AddDays(-31));

        using var response = await CancelAsync(ownerClient, ownerToken, expiredId, "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.Expired");
    }

    [Fact]
    public async Task Cancel_DoesNotMoveQueuedSubscriptions()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var today = Today();
        var currentId = await InsertSubscriptionAsync(member.Id, plan.Id, today, today.AddDays(29));
        var queuedStart = today.AddDays(30);
        var queuedEnd = queuedStart.AddDays(29);
        var queuedId = await InsertSubscriptionAsync(member.Id, plan.Id, queuedStart, queuedEnd);

        using var response = await CancelAsync(ownerClient, ownerToken, currentId, "انصراف عضو");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var queued = await GetOkAsync(ownerClient, ownerToken, queuedId);
        queued.StartDate.ShouldBe(queuedStart);
        queued.EndDate.ShouldBe(queuedEnd);
    }

    [Fact]
    public async Task Cancel_ByStaff_Returns403()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        var sold = await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);

        using var response = await CancelAsync(staffClient, staffToken, sold.Id, "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancel_UnknownId_Returns404()
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();

        using var response = await CancelAsync(ownerClient, ownerToken, Guid.CreateVersion7(), "انصراف عضو");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Auth ----

    [Theory]
    [InlineData("freeze")]
    [InlineData("unfreeze")]
    [InlineData("cancel")]
    public async Task Action_WithoutToken_Returns401(string action)
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync(
            $"/api/subscriptions/{Guid.CreateVersion7()}/{action}", null, TestContext.Current.CancellationToken);

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

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
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

    /// <summary>A row written directly, so freeze/queue states that take real days to reach can be set up in one step.</summary>
    private async Task<Guid> InsertSubscriptionAsync(
        Guid memberId, Guid planId, DateOnly start, DateOnly end, DateOnly? frozenSince = null, int totalFrozenDays = 0)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, frozen_since, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, {planId}, 900000, 30, 12,
                    {start}, {end}, 0, {frozenSince}, {totalFrozenDays}, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    /// <summary>Uses a session the only way anything does, and returns the visit's id.</summary>
    private static async Task<Guid> CheckInOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var attendance = (await response.Content.ReadFromJsonAsync<AttendanceResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return attendance.Id;
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, Guid planId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });

    private static async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await AssignAsync(client, token, memberId, planId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> FreezeAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/subscriptions/{id}/freeze");

    private static async Task<SubscriptionResponse> FreezeOkAsync(HttpClient client, string token, Guid id)
    {
        using var response = await FreezeAsync(client, token, id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> UnfreezeAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/subscriptions/{id}/unfreeze");

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/subscriptions/{id}/cancel", new { reason });

    private static async Task<SubscriptionResponse> CancelOkAsync(HttpClient client, string token, Guid id, string reason)
    {
        using var response = await CancelAsync(client, token, id, reason);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private static async Task<SubscriptionResponse> GetOkAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/subscriptions/{id}");
        response.EnsureSuccessStatusCode();

        return await ReadAsync(response);
    }

    private static async Task<SubscriptionResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

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

using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Plans;
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
/// Selling and using a single visit end to end (BUSINESS_RULES.md §3, §4, task 6.5.3). The point of
/// these tests is the damage that must <b>not</b> happen to a membership the member already paid for.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SingleSessionEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- The plan ----

    [Fact]
    public async Task CreatePlan_SingleSession_IsStoredWithItsKind()
    {
        var (client, token) = await OwnerClientAsync();

        using var response = await CreatePlanAsync(client, token, "تک‌جلسه‌ای", 1, 1, 150_000m, "SingleSession");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        plan.Kind.ShouldBe(PlanKind.SingleSession);
        plan.DurationDays.ShouldBe(1);
        plan.SessionCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreatePlan_SecondSingleSessionPlan_IsRefused()
    {
        var (client, token) = await OwnerClientAsync();
        using var first = await CreatePlanAsync(client, token, "تک‌جلسه‌ای", 1, 1, 150_000m, "SingleSession");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await CreatePlanAsync(client, token, "ورود آزاد", 1, 1, 200_000m, "SingleSession");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.SingleSessionAlreadyExists");
    }

    [Fact]
    public async Task CreatePlan_SingleSessionWithMoreThanOneSession_IsRefused()
    {
        var (client, token) = await OwnerClientAsync();

        using var response = await CreatePlanAsync(client, token, "تک‌جلسه‌ای", 1, 10, 150_000m, "SingleSession");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.SingleSessionShape");
    }

    [Fact]
    public async Task CreatePlan_ManyMemberships_AreAllAllowed()
    {
        // The partial unique index must not touch memberships.
        var (client, token) = await OwnerClientAsync();

        using var first = await CreatePlanAsync(client, token, "یک ماهه", 30, 12, 900_000m);
        using var second = await CreatePlanAsync(client, token, "سه ماهه", 90, 36, 2_400_000m);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListPlans_FilteredByKind_FindsTheSingleSessionPlanWhateverPageItIsOn()
    {
        // How the entry screen finds it (task 6.5.4): there is exactly one, it can sit on any
        // page, and paging through every plan hoping to meet it is not a way to find something.
        var (client, token) = await StaffClientAsync();
        foreach (var index in Enumerable.Range(1, 3))
        {
            await AddPlanAsync($"ماهانه {index}", 30, 12, 900_000m);
        }

        var single = await AddSingleSessionPlanAsync();

        using var response = await SendAsync(
            client, token, HttpMethod.Get, "/api/plans?kind=SingleSession&pageSize=1", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<PlanResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(single.Id);
    }

    [Fact]
    public async Task ListPlans_FilteredByMembershipKind_LeavesOutTheSingleSessionPlan()
    {
        var (client, token) = await StaffClientAsync();
        await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        await AddSingleSessionPlanAsync();

        using var response = await SendAsync(
            client, token, HttpMethod.Get, "/api/plans?kind=Membership", body: null);

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<PlanResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("ماهانه");
    }

    [Fact]
    public async Task CurrentlyInside_AfterASingleVisitCheckIn_SaysItWasASingleVisit()
    {
        // The board needs this to show "تک‌جلسه‌ای" instead of a bar that would always read
        // "۱ از ۱", and to leave off the running-out mark that would fire on every such row.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        await AssignOkAsync(client, token, member.Id, plan.Id);
        using (var checkIn = await CheckInAsync(client, token, member.Id))
        {
            checkIn.EnsureSuccessStatusCode();
        }

        using var response = await SendAsync(
            client, token, HttpMethod.Get, "/api/attendance/currently-inside", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().IsSingleSession.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrentlyInside_AfterAMembershipCheckIn_SaysItWasNotASingleVisit()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        await AssignOkAsync(client, token, member.Id, plan.Id);
        using (var checkIn = await CheckInAsync(client, token, member.Id))
        {
            checkIn.EnsureSuccessStatusCode();
        }

        using var response = await SendAsync(
            client, token, HttpMethod.Get, "/api/attendance/currently-inside", body: null);

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().IsSingleSession.ShouldBeFalse();
    }

    // ---- Selling it ----

    [Fact]
    public async Task Assign_SingleVisit_StartsAndEndsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        var today = Today();

        var visit = await AssignOkAsync(client, token, member.Id, plan.Id);

        visit.IsSingleSession.ShouldBeTrue();
        visit.StartDate.ShouldBe(today);
        visit.EndDate.ShouldBe(today);
        visit.TotalSessions.ShouldBe(1);
        visit.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Assign_SingleVisitWhileAMembershipIsCurrent_StillStartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var current = await AssignOkAsync(client, token, member.Id, membership.Id);

        var visit = await AssignOkAsync(client, token, member.Id, singleSession.Id);

        visit.StartDate.ShouldBe(Today());
        // The membership is untouched: same term, same sessions.
        var reread = await GetOkAsync(client, token, current.Id);
        reread.StartDate.ShouldBe(current.StartDate);
        reread.EndDate.ShouldBe(current.EndDate);
    }

    [Fact]
    public async Task Assign_SingleVisitWhileAMembershipIsExhausted_LeavesItsEndDateAlone()
    {
        // The bug this feature exists to avoid: closing the exhausted pack "yesterday" to make room.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه ۱ جلسه", 30, 1, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership.Id);
        await UseEverySessionAsync(pack.Id);

        var visit = await AssignOkAsync(client, token, member.Id, singleSession.Id);

        visit.StartDate.ShouldBe(Today());
        (await GetOkAsync(client, token, pack.Id)).EndDate.ShouldBe(pack.EndDate);
    }

    [Fact]
    public async Task Assign_MembershipAfterASingleVisitToday_StartsToday()
    {
        // The visitor buys a plan an hour after dropping in; it must not start tomorrow.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var singleSession = await AddSingleSessionPlanAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        await AssignOkAsync(client, token, member.Id, singleSession.Id);

        var sold = await AssignOkAsync(client, token, member.Id, membership.Id);

        sold.StartDate.ShouldBe(Today());
        sold.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Assign_TwoSingleVisitsOnTheSameDay_BothStartToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();

        var first = await AssignOkAsync(client, token, member.Id, plan.Id);
        var second = await AssignOkAsync(client, token, member.Id, plan.Id);

        first.StartDate.ShouldBe(Today());
        second.StartDate.ShouldBe(Today());
        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task Assign_SingleVisitWhileTheMembershipIsFrozen_SucceedsAndLeavesTheFreezeAlone()
    {
        // What the front desk does for a member who is away and drops in once. Freeze and unfreeze
        // stay Owner-only (BUSINESS_RULES.md §1); the desk does not need either of them.
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var pack = await AssignOkAsync(staff, staffToken, member.Id, membership.Id);
        using var frozen = await SendAsync(owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{pack.Id}/freeze");
        frozen.EnsureSuccessStatusCode();

        var visit = await AssignOkAsync(staff, staffToken, member.Id, singleSession.Id);

        visit.StartDate.ShouldBe(Today());
        var reread = await GetOkAsync(owner, ownerToken, pack.Id);
        reread.Status.ShouldBe(SubscriptionStatus.Frozen);
        reread.EndDate.ShouldBe(pack.EndDate);
    }

    // ---- Freeze and renew ----

    [Fact]
    public async Task Freeze_SingleVisit_IsRefused()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        var visit = await AssignOkAsync(staff, staffToken, member.Id, plan.Id);

        using var response = await SendAsync(owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{visit.Id}/freeze");

        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.SingleSessionNotFreezable");
    }

    [Fact]
    public async Task Renew_AfterOnlyASingleVisit_HasNothingToRenew()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        await AssignOkAsync(client, token, member.Id, plan.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions/renew");

        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NothingToRenew");
    }

    [Fact]
    public async Task Renew_WithAMembershipAndALaterSingleVisit_RenewsTheMembership()
    {
        // The single visit ends today and the expired membership ended earlier, so "latest by end
        // date" would pick the visit. Renew reads memberships only.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership.Id);
        await ExpireAsync(pack.Id);
        await AssignOkAsync(client, token, member.Id, singleSession.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions/renew");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var renewed = (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        renewed.PlanId.ShouldBe(membership.Id);
        renewed.IsSingleSession.ShouldBeFalse();
    }

    // ---- Money: what §4 says is unchanged, proved rather than assumed ----

    [Fact]
    public async Task Debt_UnpaidSingleVisit_AppearsInTheBreakdown()
    {
        // A single visit is an ordinary subscription, so the open account (§5) picks it up with no
        // code of its own. That is the whole argument for this design, so it gets a test.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        var visit = await AssignOkAsync(client, token, member.Id, plan.Id);

        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{member.Id}/debt");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var debt = (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        debt.Total.ShouldBe(150_000m);
        debt.Items.ShouldContain(item => item.Id == visit.Id && item.Outstanding == 150_000m);
    }

    [Fact]
    public async Task Cancel_SingleVisitBeforeItIsUsed_IsAllowed()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        var visit = await AssignOkAsync(staff, staffToken, member.Id, plan.Id);

        using var response = await SendAsync(
            owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{visit.Id}/cancel",
            new { reason = "پشیمان شد و رفت" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(owner, ownerToken, visit.Id)).Status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_SingleVisitAfterTheMemberIsInside_IsRefused()
    {
        // §4 Cancel: a used session is not un-sold. The 30-minute cancel-check-in window is the
        // escape hatch, not this.
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        var visit = await AssignOkAsync(staff, staffToken, member.Id, plan.Id);
        using var checkedIn = await CheckInAsync(staff, staffToken, member.Id);
        checkedIn.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await SendAsync(
            owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{visit.Id}/cancel",
            new { reason = "اشتباه ثبت شد" });

        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.AlreadyUsed");
    }

    [Fact]
    public async Task UpdatePlan_ChangingTheWalkInRate_KeepsTheKind()
    {
        // BUSINESS_RULES.md §3: editing this plan's price is the whole mechanism for changing the
        // rate, and the kind is set once and never edited.
        var (client, token) = await OwnerClientAsync();
        using var created = await CreatePlanAsync(client, token, "تک‌جلسه‌ای", 1, 1, 150_000m, "SingleSession");
        var plan = (await created.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();

        using var response = await SendAsync(
            client, token, HttpMethod.Put, $"/api/plans/{plan.Id}",
            new { name = "تک‌جلسه‌ای", durationDays = 1, sessionCount = 1, price = 200_000m, version = plan.Version });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        updated.Price.ShouldBe(200_000m);
        updated.Kind.ShouldBe(PlanKind.SingleSession);
    }

    // ---- Using it ----

    [Fact]
    public async Task CheckIn_OnASingleVisit_ConsumesThatVisitOnly()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership.Id);
        var visit = await AssignOkAsync(client, token, member.Id, singleSession.Id);

        using var response = await CheckInAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await GetOkAsync(client, token, visit.Id)).UsedSessions.ShouldBe(1);
        (await GetOkAsync(client, token, pack.Id)).UsedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task CheckIn_TwiceInOneDayOnTwoSingleVisits_BothSucceed()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        await AssignOkAsync(client, token, member.Id, plan.Id);
        await AssignOkAsync(client, token, member.Id, plan.Id);

        await CheckInAndOutAsync(client, token, member.Id);

        using var second = await CheckInAsync(client, token, member.Id);

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CheckIn_SecondVisitOfTheDayWithNothingLeft_IsRefused()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddSingleSessionPlanAsync();
        await AssignOkAsync(client, token, member.Id, plan.Id);
        await CheckInAndOutAsync(client, token, member.Id);

        using var response = await CheckInAsync(client, token, member.Id);

        // Not "come back tomorrow": there is no queued membership behind this, only a used visit.
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NoSessionsLeft");
    }

    [Fact]
    public async Task CheckIn_UsedSingleVisitWithAQueuedMembership_DoesNotPullTheMembershipForward()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await AddPlanAsync("ماهانه", 30, 12, 900_000m);
        var singleSession = await AddSingleSessionPlanAsync();
        var current = await AssignOkAsync(client, token, member.Id, membership.Id);
        var queued = await AssignOkAsync(client, token, member.Id, membership.Id);
        await ExpireAsync(current.Id);
        await AssignOkAsync(client, token, member.Id, singleSession.Id);
        await CheckInAndOutAsync(client, token, member.Id);

        using var response = await CheckInAsync(client, token, member.Id);

        // "It has not started yet", naming the queued membership — not "come back tomorrow", which is
        // reserved for a membership that ran out of sessions on its own first day.
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NotStarted");
        var stillQueued = await GetOkAsync(client, token, queued.Id);
        stillQueued.StartDate.ShouldBe(queued.StartDate);
        stillQueued.Status.ShouldBe(SubscriptionStatus.Upcoming);
    }

    // ---- Helpers ----

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private Task<Plan> AddSingleSessionPlanAsync() =>
        AddPlanAsync("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession);

    private async Task<Plan> AddPlanAsync(
        string name, int durationDays, int? sessions, decimal price, PlanKind kind = PlanKind.Membership)
    {
        var plan = Plan.Create(name, durationDays, sessions, price, kind).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    /// <summary>Uses every session, which is what makes a subscription <c>Exhausted</c>.</summary>
    private async Task UseEverySessionAsync(Guid subscriptionId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET used_sessions = total_sessions WHERE id = {subscriptionId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>Moves a subscription entirely into the past, which no endpoint can do.</summary>
    private async Task ExpireAsync(Guid subscriptionId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE subscriptions
            SET start_date = start_date - INTERVAL '60 days', end_date = end_date - INTERVAL '60 days'
            WHERE id = {subscriptionId}
            """,
            TestContext.Current.CancellationToken);
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

    private static Task<HttpResponseMessage> CreatePlanAsync(
        HttpClient client, string token, string name, int durationDays, int? sessionCount, decimal price,
        string? kind = null)
    {
        object body = kind is null
            ? new { name, durationDays, sessionCount, price }
            : new { name, durationDays, sessionCount, price, kind };

        return SendAsync(client, token, HttpMethod.Post, "/api/plans", body);
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");

    /// <summary>A whole visit: in and out again, which is what frees the member to come back.</summary>
    private static async Task CheckInAndOutAsync(HttpClient client, string token, Guid memberId)
    {
        using var checkedIn = await CheckInAsync(client, token, memberId);
        checkedIn.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = (await checkedIn.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();

        using var checkedOut = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out");
        checkedOut.EnsureSuccessStatusCode();
    }

    private static async Task<SubscriptionResponse> AssignOkAsync(
        HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private static async Task<SubscriptionResponse> GetOkAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/subscriptions/{id}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}

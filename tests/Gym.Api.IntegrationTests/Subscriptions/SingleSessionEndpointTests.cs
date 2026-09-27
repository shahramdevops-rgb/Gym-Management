using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// Selling and using a single visit end to end: <c>POST /api/members/{id}/subscriptions/single-visit</c>
/// (BUSINESS_RULES.md §3, §4; tasks 6.5.3 and 6.5.6). The point of these tests is the damage that must
/// <b>not</b> happen to a membership the member already paid for.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SingleSessionEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const decimal SingleVisitPrice = 150_000m;

    private static int _phoneSuffix;

    // ---- Selling it ----

    [Fact]
    public async Task SellSingleVisit_PriceSet_IsOneSessionForTodayAtThatPrice()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        var today = Today();

        using var response = await SellVisitAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/api/subscriptions/{visit.Id}");
        visit.IsSingleSession.ShouldBeTrue();
        visit.Price.ShouldBe(SingleVisitPrice);
        visit.StartDate.ShouldBe(today);
        visit.EndDate.ShouldBe(today);
        visit.DurationDays.ShouldBe(1);
        visit.TotalSessions.ShouldBe(1);
        visit.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task SellSingleVisit_PriceNotSet_Returns422SingleVisitPriceNotSet()
    {
        // BUSINESS_RULES.md §3: both prices start empty, and nothing sells at a price nobody chose.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await SellVisitAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Pricing.SingleVisitPriceNotSet");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SellSingleVisit_PriceChangedAfterwards_KeepsWhatTheVisitCost()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        var visit = await SellVisitOkAsync(client, token, member.Id);

        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 200_000m);

        (await GetOkAsync(client, token, visit.Id)).Price.ShouldBe(SingleVisitPrice);
    }

    [Fact]
    public async Task SellSingleVisit_InactiveMember_Returns422MemberInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync(active: false);
        await SetSingleVisitPriceAsync();

        using var response = await SellVisitAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
    }

    [Fact]
    public async Task SellSingleVisit_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        await SetSingleVisitPriceAsync();

        using var response = await SellVisitAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task SellSingleVisit_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync(
            $"/api/members/{Guid.CreateVersion7()}/subscriptions/single-visit", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SellSingleVisit_WhileAMembershipIsCurrent_StillStartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        var current = await AssignOkAsync(client, token, member.Id, membership);

        var visit = await SellVisitOkAsync(client, token, member.Id);

        visit.StartDate.ShouldBe(Today());
        // The membership is untouched: same term, same sessions.
        var reread = await GetOkAsync(client, token, current.Id);
        reread.StartDate.ShouldBe(current.StartDate);
        reread.EndDate.ShouldBe(current.EndDate);
    }

    [Fact]
    public async Task SellSingleVisit_WhileAMembershipIsExhausted_LeavesItsEndDateAlone()
    {
        // The bug this feature exists to avoid: closing the exhausted pack "yesterday" to make room.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture, sessions: 5, price: 500_000m);
        await SetSingleVisitPriceAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership);
        await UseEverySessionAsync(pack.Id);

        var visit = await SellVisitOkAsync(client, token, member.Id);

        visit.StartDate.ShouldBe(Today());
        (await GetOkAsync(client, token, pack.Id)).EndDate.ShouldBe(pack.EndDate);
    }

    [Fact]
    public async Task Assign_MembershipAfterASingleVisitToday_StartsToday()
    {
        // The visitor buys a plan an hour after dropping in; it must not start tomorrow.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        await SellVisitOkAsync(client, token, member.Id);

        var sold = await AssignOkAsync(client, token, member.Id, membership);

        sold.StartDate.ShouldBe(Today());
        sold.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task SellSingleVisit_TwiceOnTheSameDay_BothStartToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();

        var first = await SellVisitOkAsync(client, token, member.Id);
        var second = await SellVisitOkAsync(client, token, member.Id);

        first.StartDate.ShouldBe(Today());
        second.StartDate.ShouldBe(Today());
        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task SellSingleVisit_WhileTheMembershipIsFrozen_SucceedsAndLeavesTheFreezeAlone()
    {
        // What the front desk does for a member who is away and drops in once. Freeze and unfreeze
        // stay Owner-only (BUSINESS_RULES.md §1); the desk does not need either of them.
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        var pack = await AssignOkAsync(staff, staffToken, member.Id, membership);
        using var frozen = await SendAsync(owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{pack.Id}/freeze");
        frozen.EnsureSuccessStatusCode();

        var visit = await SellVisitOkAsync(staff, staffToken, member.Id);

        visit.StartDate.ShouldBe(Today());
        var reread = await GetOkAsync(owner, ownerToken, pack.Id);
        reread.Status.ShouldBe(SubscriptionStatus.Frozen);
        reread.EndDate.ShouldBe(pack.EndDate);
    }

    [Fact]
    public async Task CurrentlyInside_AfterASingleVisitCheckIn_SaysItWasASingleVisit()
    {
        // The board needs this to show "تک‌جلسه‌ای" instead of a bar that would always read
        // "۱ از ۱", and to leave off the running-out mark that would fire on every such row.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        await SellVisitOkAsync(client, token, member.Id);
        using (var checkIn = await CheckInAsync(client, token, member.Id))
        {
            checkIn.EnsureSuccessStatusCode();
        }

        var page = await CurrentlyInsideAsync(client, token);

        page.Items.ShouldHaveSingleItem().IsSingleSession.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrentlyInside_AfterAMembershipCheckIn_SaysItWasNotASingleVisit()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture);
        await AssignOkAsync(client, token, member.Id, membership);
        using (var checkIn = await CheckInAsync(client, token, member.Id))
        {
            checkIn.EnsureSuccessStatusCode();
        }

        var page = await CurrentlyInsideAsync(client, token);

        page.Items.ShouldHaveSingleItem().IsSingleSession.ShouldBeFalse();
    }

    // ---- Freeze and renew ----

    [Fact]
    public async Task Freeze_SingleVisit_IsRefused()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        var visit = await SellVisitOkAsync(staff, staffToken, member.Id);

        using var response = await SendAsync(owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{visit.Id}/freeze");

        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.SingleSessionNotFreezable");
    }

    [Fact]
    public async Task Renew_AfterOnlyASingleVisit_HasNothingToRenew()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        await SellVisitOkAsync(client, token, member.Id);

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
        var membership = await TestPlans.AddAsync(Fixture, durationDays: 45, sessions: 12);
        await SetSingleVisitPriceAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership);
        await ExpireAsync(pack.Id);
        await SellVisitOkAsync(client, token, member.Id);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions/renew");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var renewed = (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        renewed.IsSingleSession.ShouldBeFalse();
        renewed.DurationDays.ShouldBe(45);
        renewed.TotalSessions.ShouldBe(12);
    }

    // ---- Money: what §4 says is unchanged, proved rather than assumed ----

    [Fact]
    public async Task Debt_UnpaidSingleVisit_AppearsInTheBreakdown()
    {
        // A single visit is an ordinary subscription, so the open account (§5) picks it up with no
        // code of its own. That is the whole argument for this design, so it gets a test.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        var visit = await SellVisitOkAsync(client, token, member.Id);

        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{member.Id}/debt");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var debt = (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        debt.Total.ShouldBe(SingleVisitPrice);
        var item = debt.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(visit.Id);
        item.Outstanding.ShouldBe(SingleVisitPrice);
        item.Plan.ShouldBe(new PlanSummary(DurationDays: 1, TotalSessions: 1, IsSingleSession: true));
    }

    [Fact]
    public async Task Cancel_SingleVisitBeforeItIsUsed_IsAllowed()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        var visit = await SellVisitOkAsync(staff, staffToken, member.Id);

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
        await SetSingleVisitPriceAsync();
        var visit = await SellVisitOkAsync(staff, staffToken, member.Id);
        using var checkedIn = await CheckInAsync(staff, staffToken, member.Id);
        checkedIn.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await SendAsync(
            owner, ownerToken, HttpMethod.Post, $"/api/subscriptions/{visit.Id}/cancel",
            new { reason = "اشتباه ثبت شد" });

        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.AlreadyUsed");
    }

    // ---- Using it ----

    [Fact]
    public async Task CheckIn_OnASingleVisit_ConsumesThatVisitOnly()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var membership = await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        var pack = await AssignOkAsync(client, token, member.Id, membership);
        var visit = await SellVisitOkAsync(client, token, member.Id);

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
        await SetSingleVisitPriceAsync();
        await SellVisitOkAsync(client, token, member.Id);
        await SellVisitOkAsync(client, token, member.Id);

        await CheckInAndOutAsync(client, token, member.Id);

        using var second = await CheckInAsync(client, token, member.Id);

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CheckIn_SecondVisitOfTheDayWithNothingLeft_IsRefused()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await SetSingleVisitPriceAsync();
        await SellVisitOkAsync(client, token, member.Id);
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
        var membership = await TestPlans.AddAsync(Fixture);
        await SetSingleVisitPriceAsync();
        var current = await AssignOkAsync(client, token, member.Id, membership);
        var queued = await AssignOkAsync(client, token, member.Id, membership);
        await ExpireAsync(current.Id);
        await SellVisitOkAsync(client, token, member.Id);
        await CheckInAndOutAsync(client, token, member.Id);

        using var response = await CheckInAsync(client, token, member.Id);

        // "It has not started yet", naming the queued membership — not "come back tomorrow", which is
        // reserved for a membership that ran out of sessions on its own first day.
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NotStarted");
        var stillQueued = await GetOkAsync(client, token, queued.Id);
        stillQueued.StartDate.ShouldBe(queued.StartDate);
        stillQueued.Status.ShouldBe(SubscriptionStatus.Upcoming);
    }

    // ---- Database ----

    [Fact]
    public async Task Database_SingleSessionRowWithTwoSessions_IsRefused()
    {
        var member = await AddMemberAsync();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var insert = () => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions, is_single_session,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({Guid.CreateVersion7()}, {member.Id}, 150000, 1, 2, true, {Today()}, {Today()}, 0, 0, now())
            """,
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<Npgsql.PostgresException>(insert)).ConstraintName
            .ShouldBe("ck_subscriptions_single_session_shape");
    }

    // ---- Helpers ----

    private Task SetSingleVisitPriceAsync() => TestPlans.SetPricesAsync(Fixture, singleVisitPrice: SingleVisitPrice);

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync(bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");
        if (!active)
        {
            member.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
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

    private async Task<int> CountSubscriptionsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .CountAsync(TestContext.Current.CancellationToken);
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

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        TestLockers.CheckInAsync(client, token, memberId);

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

    private static async Task<PagedResponse<CurrentlyInsideResponse>> CurrentlyInsideAsync(HttpClient client, string token)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, "/api/attendance/currently-inside");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<CurrentlyInsideResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SellVisitAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/single-visit");

    private static async Task<SubscriptionResponse> SellVisitOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SellVisitAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private static async Task<SubscriptionResponse> AssignOkAsync(
        HttpClient client, string token, Guid memberId, TestPlan plan)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
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

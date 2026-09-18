using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
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
/// Selling subscriptions: <c>POST /api/members/{id}/subscriptions</c>, <c>/renew</c> and
/// <c>GET /api/subscriptions/{id}</c> (BUSINESS_RULES.md §4). "Today" is read from the app's own
/// <see cref="IGymCalendar"/>, so the tests agree with the handlers on the date.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Assign ----

    [Fact]
    public async Task Assign_NothingCurrent_StartsTodayWithTheSnapshot()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("یک ماهه ۱۲ جلسه", 30, 12, 900_000m);
        var today = Today();

        using var response = await AssignAsync(client, token, member.Id, plan.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var subscription = await ReadAsync(response);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/api/subscriptions/{subscription.Id}");
        subscription.MemberId.ShouldBe(member.Id);
        subscription.PlanId.ShouldBe(plan.Id);
        subscription.PlanName.ShouldBe("یک ماهه ۱۲ جلسه");
        subscription.Price.ShouldBe(900_000m);
        subscription.TotalSessions.ShouldBe(12);
        subscription.RemainingSessions.ShouldBe(12);
        subscription.StartDate.ShouldBe(today);
        subscription.EndDate.ShouldBe(today.AddDays(29));
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Assign_WhileOneIsCurrent_IsQueuedTheDayAfterItEnds()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);
        var current = await AssignOkAsync(client, token, member.Id, plan.Id);

        var queued = await AssignOkAsync(client, token, member.Id, plan.Id);

        queued.StartDate.ShouldBe(current.EndDate.AddDays(1));
        queued.Status.ShouldBe(SubscriptionStatus.Upcoming);
    }

    [Fact]
    public async Task Assign_TwoQueued_TheSecondGoesAfterTheFirst()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);
        await AssignOkAsync(client, token, member.Id, plan.Id);
        var first = await AssignOkAsync(client, token, member.Id, plan.Id);

        var second = await AssignOkAsync(client, token, member.Id, plan.Id);

        second.StartDate.ShouldBe(first.EndDate.AddDays(1));
    }

    [Fact]
    public async Task Assign_PlanEditedAfterTheSale_KeepsTheSnapshot()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("یک ماهه", 30, 12, 900_000m);
        var sold = await AssignOkAsync(client, token, member.Id, plan.Id);

        await ChangePlanAsync(plan.Id, p => p.Update("یک ماهه جدید", 60, null, 1_200_000m));

        var read = await GetOkAsync(client, token, sold.Id);
        read.PlanName.ShouldBe("یک ماهه");
        read.Price.ShouldBe(900_000m);
        read.DurationDays.ShouldBe(30);
        read.TotalSessions.ShouldBe(12);
    }

    [Fact]
    public async Task Assign_CurrentOneCancelled_StartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);
        var cancelled = await AssignOkAsync(client, token, member.Id, plan.Id);
        await ChangeSubscriptionAsync(cancelled.Id, s => s.Cancel("انصراف عضو", DateTimeOffset.UtcNow));

        var next = await AssignOkAsync(client, token, member.Id, plan.Id);

        next.StartDate.ShouldBe(Today());
    }

    [Fact]
    public async Task Assign_CurrentOneExhausted_ClosesItYesterdayAndStartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("یک جلسه", 30, 1, 100_000m);
        var today = Today();

        // Sold yesterday and fully used, so it can end yesterday.
        var exhaustedId = await InsertSubscriptionAsync(member.Id, plan.Id, today.AddDays(-1), today.AddDays(28), totalSessions: 1, usedSessions: 1);

        var next = await AssignOkAsync(client, token, member.Id, plan.Id);

        next.StartDate.ShouldBe(today);
        (await GetOkAsync(client, token, exhaustedId)).EndDate.ShouldBe(today.AddDays(-1));
    }

    [Fact]
    public async Task Assign_InactivePlan_Returns422PlanInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("قدیمی", 30, 12, 900_000m, active: false);

        using var response = await AssignAsync(client, token, member.Id, plan.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.Inactive");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_InactiveMember_Returns422MemberInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync(active: false);
        var plan = await AddPlanAsync("ماهانه", 30, 12, 900_000m);

        using var response = await AssignAsync(client, token, member.Id, plan.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var plan = await AddPlanAsync("ماهانه", 30, 12, 900_000m);

        using var response = await AssignAsync(client, token, Guid.CreateVersion7(), plan.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task Assign_UnknownPlan_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await AssignAsync(client, token, member.Id, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NotFound");
    }

    [Fact]
    public async Task Assign_EmptyPlanId_Returns400WithFieldCode()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await AssignAsync(client, token, member.Id, Guid.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("planId")[0].GetProperty("code").GetString()
            .ShouldBe("Subscriptions.PlanRequired");
    }

    [Fact]
    public async Task Assign_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/members/{Guid.CreateVersion7()}/subscriptions",
            new { planId = Guid.CreateVersion7() },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_ManyInParallelForOneMember_AllSucceedOneAfterAnother()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("ماهانه", 30, null, 900_000m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => AssignAsync(client, token, member.Id, plan.Id)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        // The member lock makes the sales take turns: every one succeeds and each is queued the
        // day after the one before it, with no gap and no overlap.
        statuses.ShouldAllBe(status => status == HttpStatusCode.Created);

        var ordered = (await SubscriptionsOfAsync(member.Id)).OrderBy(s => s.StartDate).ToList();
        ordered.Count.ShouldBe(6);
        ordered[0].StartDate.ShouldBe(Today());
        for (var i = 1; i < ordered.Count; i++)
        {
            ordered[i].StartDate.ShouldBe(ordered[i - 1].EndDate.AddDays(1));
        }
    }

    // ---- Renew ----

    [Fact]
    public async Task Renew_AfterAPlanPriceChange_SellsTheSamePlanAtTheCurrentPrice()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("یک ماهه", 30, 12, 900_000m);
        var first = await AssignOkAsync(client, token, member.Id, plan.Id);
        await ChangePlanAsync(plan.Id, p => p.Update("یک ماهه", 30, 12, 950_000m));

        using var response = await RenewAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var renewed = await ReadAsync(response);
        renewed.PlanId.ShouldBe(plan.Id);
        renewed.Price.ShouldBe(950_000m);
        renewed.StartDate.ShouldBe(first.EndDate.AddDays(1));
    }

    [Fact]
    public async Task Renew_NoSubscriptionYet_Returns422NothingToRenew()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await RenewAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NothingToRenew");
    }

    [Fact]
    public async Task Renew_LatestPlanNowInactive_Returns422PlanInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync("قدیمی", 30, 12, 900_000m);
        await AssignOkAsync(client, token, member.Id, plan.Id);
        await ChangePlanAsync(plan.Id, p => p.Deactivate());

        using var response = await RenewAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.Inactive");
    }

    [Fact]
    public async Task Renew_InactiveMember_Returns422MemberInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync(active: false);

        using var response = await RenewAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
    }

    [Fact]
    public async Task Renew_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await RenewAsync(client, token, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Get ----

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/subscriptions/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Subscriptions.NotFound");
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

    private async Task<Member> AddMemberAsync(bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Member.Create("رضا احمدی", $"+98912{suffix:D7}", null).Value;
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

    private async Task<Plan> AddPlanAsync(string name, int durationDays, int? sessions, decimal price, bool active = true)
    {
        var plan = Plan.Create(name, durationDays, sessions, price).Value;
        if (!active)
        {
            plan.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private async Task ChangePlanAsync(Guid id, Action<Plan> change)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(await db.Plans.SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ChangeSubscriptionAsync(Guid id, Action<Subscription> change)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(await db.Subscriptions.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A row written directly, for states the API cannot create yet (sessions are used by check-in, Phase 5).</summary>
    private async Task<Guid> InsertSubscriptionAsync(
        Guid memberId, Guid planId, DateOnly start, DateOnly end, int? totalSessions, int usedSessions)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, plan_name, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, {planId}, 'پلن', 100000, 30, {totalSessions},
                    {start}, {end}, {usedSessions}, 0, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<List<Subscription>> SubscriptionsOfAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountSubscriptionsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, Guid planId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });

    private static async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await AssignAsync(client, token, memberId, planId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> RenewAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/renew");

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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// Selling subscriptions: <c>POST /api/members/{id}/subscriptions</c>, <c>/renew</c> and
/// <c>GET /api/subscriptions/{id}</c> (BUSINESS_RULES.md §3, §4). "Today" is read from the app's own
/// <see cref="IGymCalendar"/>, so the tests agree with the handlers on the date.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Assign ----

    [Fact]
    public async Task Assign_NothingCurrent_StartsTodayAtSessionsTimesTheSessionPrice()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 75_000m);
        var today = Today();

        using var response = await AssignAsync(client, token, member.Id, sessionCount: 12);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var subscription = await ReadAsync(response);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/api/subscriptions/{subscription.Id}");
        subscription.MemberId.ShouldBe(member.Id);
        subscription.Price.ShouldBe(900_000m);
        subscription.DurationDays.ShouldBe(45);
        subscription.TotalSessions.ShouldBe(12);
        subscription.RemainingSessions.ShouldBe(12);
        subscription.IsSingleSession.ShouldBeFalse();
        subscription.StartDate.ShouldBe(today);
        subscription.EndDate.ShouldBe(today.AddDays(44));
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Theory]
    [InlineData(5, 30)]
    [InlineData(10, 30)]
    [InlineData(11, 45)]
    [InlineData(20, 45)]
    [InlineData(21, 70)]
    [InlineData(140, 70)]
    public async Task Assign_SessionCount_TheServerWorksOutTheDays(int sessions, int expectedDays)
    {
        // BUSINESS_RULES.md §3 (task 6.5.18): the desk sends only the sessions.
        var (client, token) = await StaffClientAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 75_000m);

        var sold = await AssignOkAsync(client, token, (await AddMemberAsync()).Id, sessionCount: sessions);

        sold.DurationDays.ShouldBe(expectedDays);
        sold.EndDate.ShouldBe(sold.StartDate.AddDays(expectedDays - 1));
        sold.Price.ShouldBe(sessions * 75_000m);
    }

    [Fact]
    public async Task Assign_DaysSentByAnOldClient_AreIgnored()
    {
        // The days are not the desk's to choose any more; a body that still carries them sells
        // the days the sessions give.
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 75_000m);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions", new { durationDays = 90, sessionCount = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsync(response)).DurationDays.ShouldBe(45);
    }

    [Fact]
    public async Task Assign_SessionPriceNotSet_Returns422SessionPriceNotSet()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();

        using var response = await AssignAsync(client, token, member.Id, sessionCount: 12);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Pricing.SessionPriceNotSet");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_PriceChangedAfterTheSale_KeepsWhatWasSold()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var sold = await AssignOkAsync(client, token, member.Id, plan);

        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 200_000m);

        // BUSINESS_RULES.md §3: a price change never reaches a past sale.
        var read = await GetOkAsync(client, token, sold.Id);
        read.Price.ShouldBe(900_000m);
        read.DurationDays.ShouldBe(30);
        read.TotalSessions.ShouldBe(10);
    }

    [Fact]
    public async Task Assign_FewerThanFiveSessions_Returns400WithFieldCode()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await TestPlans.AddAsync(Fixture);

        using var response = await AssignAsync(client, token, member.Id, sessionCount: 4);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "sessionCount")).ShouldBe("Subscriptions.SessionCountTooLow");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_MoreThan140Sessions_Returns400WithFieldCode()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        await TestPlans.AddAsync(Fixture);

        using var response = await AssignAsync(client, token, member.Id, sessionCount: 141);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "sessionCount")).ShouldBe("Subscriptions.SessionCountTooHigh");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_WhileOneIsCurrent_IsQueuedTheDayAfterItEnds()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var current = await AssignOkAsync(client, token, member.Id, plan);

        var queued = await AssignOkAsync(client, token, member.Id, plan);

        queued.StartDate.ShouldBe(current.EndDate.AddDays(1));
        queued.Status.ShouldBe(SubscriptionStatus.Upcoming);
    }

    [Fact]
    public async Task Assign_TwoQueued_TheSecondGoesAfterTheFirst()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        await AssignOkAsync(client, token, member.Id, plan);
        var first = await AssignOkAsync(client, token, member.Id, plan);

        var second = await AssignOkAsync(client, token, member.Id, plan);

        second.StartDate.ShouldBe(first.EndDate.AddDays(1));
    }

    [Fact]
    public async Task Sold_SubscriptionHistoryAndPaymentHistory_BothCarryWhatWasSold()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture, sessions: 12);
        var sold = await AssignOkAsync(client, token, member.Id, plan);
        await PayAsync(client, token, sold.Id, 900_000m);

        // The two list endpoints the member profile is built from: each has the numbers the
        // frontend turns into «۴۵ روز · ۱۲ جلسه» (BUSINESS_RULES.md §3).
        var subscriptions = await GetListAsync<SubscriptionResponse>(
            client, token, $"/api/members/{member.Id}/subscriptions");
        var row = subscriptions.Single();
        row.DurationDays.ShouldBe(45);
        row.TotalSessions.ShouldBe(12);

        var payments = await GetListAsync<PaymentHistoryResponse>(
            client, token, $"/api/members/{member.Id}/payments");
        payments.Single().SubscriptionPlan.ShouldBe(new PlanSummary(45, 12, IsSingleSession: false));
    }

    [Fact]
    public async Task Assign_CurrentOneCancelled_StartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var cancelled = await AssignOkAsync(client, token, member.Id, plan);
        await ChangeSubscriptionAsync(cancelled.Id, s => s.Cancel("انصراف عضو", Today(), DateTimeOffset.UtcNow));

        var next = await AssignOkAsync(client, token, member.Id, plan);

        next.StartDate.ShouldBe(Today());
    }

    [Fact]
    public async Task Assign_CurrentOneExhausted_ClosesItYesterdayAndStartsToday()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var today = Today();

        // Sold yesterday and fully used, so it can end yesterday.
        var exhaustedId = await InsertSubscriptionAsync(member.Id, today.AddDays(-1), today.AddDays(28), totalSessions: 5, usedSessions: 5);

        var next = await AssignOkAsync(client, token, member.Id, plan);

        next.StartDate.ShouldBe(today);
        (await GetOkAsync(client, token, exhaustedId)).EndDate.ShouldBe(today.AddDays(-1));
    }

    [Fact]
    public async Task Assign_InactiveMember_Returns422MemberInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync(active: false);
        var plan = await TestPlans.AddAsync(Fixture);

        using var response = await AssignAsync(client, token, member.Id, plan);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.Inactive");
        (await CountSubscriptionsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Assign_UnknownMember_Returns404()
    {
        var (client, token) = await StaffClientAsync();
        var plan = await TestPlans.AddAsync(Fixture);

        using var response = await AssignAsync(client, token, Guid.CreateVersion7(), plan);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task Assign_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/members/{Guid.CreateVersion7()}/subscriptions",
            new { sessionCount = 12 },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_ManyInParallelForOneMember_AllSucceedOneAfterAnother()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => AssignAsync(client, token, member.Id, plan)));

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
    public async Task Renew_AfterASessionPriceChange_SellsTheSameSessionsAtTodaysPrice()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture, sessions: 12);
        var first = await AssignOkAsync(client, token, member.Id, plan);
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 80_000m);

        using var response = await RenewAsync(client, token, member.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var renewed = await ReadAsync(response);
        renewed.DurationDays.ShouldBe(45);
        renewed.TotalSessions.ShouldBe(12);
        renewed.Price.ShouldBe(960_000m);
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

    // ---- Payment status (task 4.6) ----

    [Fact]
    public async Task Get_NewlySold_HasZeroNetPaidAndUnpaidStatus()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);

        var sold = await AssignOkAsync(client, token, member.Id, plan);

        sold.NetPaid.ShouldBe(0m);
        sold.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task Get_AfterPaymentThenRefund_RecalculatesFromUnpaidToPaidToPartial()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var sold = await AssignOkAsync(client, token, member.Id, plan);
        (await GetOkAsync(client, token, sold.Id)).PaymentStatus.ShouldBe(PaymentStatus.Unpaid);

        await PayAsync(client, token, sold.Id, 900_000m);
        (await GetOkAsync(client, token, sold.Id)).PaymentStatus.ShouldBe(PaymentStatus.Paid);

        await RefundAsync(sold.Id, 300_000m);
        var afterRefund = await GetOkAsync(client, token, sold.Id);
        afterRefund.NetPaid.ShouldBe(600_000m);
        afterRefund.PaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    // ---- Database ----

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    public async Task Database_MembershipWithFewerThanFiveSessions_IsRefused(int sessions)
    {
        var member = await AddMemberAsync();

        // Straight SQL, past the entity and the validator: the constraint is the last line (§3).
        var insert = () => InsertSubscriptionAsync(member.Id, Today(), Today().AddDays(29), totalSessions: sessions, usedSessions: 0);

        (await Should.ThrowAsync<Npgsql.PostgresException>(insert)).ConstraintName
            .ShouldBe("ck_subscriptions_total_sessions_range");
    }

    [Fact]
    public async Task Database_MembershipWithMoreThan140Sessions_IsRefused()
    {
        var member = await AddMemberAsync();

        var insert = () => InsertSubscriptionAsync(member.Id, Today(), Today().AddDays(69), totalSessions: 141, usedSessions: 0, durationDays: 70);

        (await Should.ThrowAsync<Npgsql.PostgresException>(insert)).ConstraintName
            .ShouldBe("ck_subscriptions_total_sessions_range");
    }

    [Theory]
    [InlineData(12, 30)]
    [InlineData(10, 45)]
    [InlineData(21, 365)]
    public async Task Database_DaysThatDoNotMatchTheSessions_AreRefused(int sessions, int days)
    {
        var member = await AddMemberAsync();

        // BUSINESS_RULES.md §3 (task 6.5.18): the days follow from the sessions.
        var insert = () => InsertSubscriptionAsync(
            member.Id, Today(), Today().AddDays(days - 1), totalSessions: sessions, usedSessions: 0, durationDays: days);

        (await Should.ThrowAsync<Npgsql.PostgresException>(insert)).ConstraintName
            .ShouldBe("ck_subscriptions_duration_days_for_sessions");
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

    private static async Task PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments",
            new { amount, method = "Cash" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task RefundAsync(Guid subscriptionId, decimal amount)
    {
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        using var response = await SendAsync(
            ownerClient, ownerToken, HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/refunds",
            new { amount, method = "Cash", reason = "بازگشت جزئی" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync(bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98912{suffix:D7}");
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

    private async Task ChangeSubscriptionAsync(Guid id, Action<Subscription> change)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(await db.Subscriptions.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A row written directly, for states the API cannot create in one step.</summary>
    private async Task<Guid> InsertSubscriptionAsync(
        Guid memberId, DateOnly start, DateOnly end, int totalSessions, int usedSessions, int durationDays = 30)
    {
        var id = Guid.CreateVersion7();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days, created_at)
            VALUES ({id}, {memberId}, 100000, {durationDays}, {totalSessions},
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

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, TestPlan plan) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);

    private static Task<HttpResponseMessage> AssignAsync(
        HttpClient client, string token, Guid memberId, int sessionCount) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { sessionCount });

    private static async Task<SubscriptionResponse> AssignOkAsync(HttpClient client, string token, Guid memberId, TestPlan plan)
    {
        using var response = await AssignAsync(client, token, memberId, plan);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    private static async Task<SubscriptionResponse> AssignOkAsync(
        HttpClient client, string token, Guid memberId, int sessionCount)
    {
        using var response = await AssignAsync(client, token, memberId, sessionCount);
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

    private static async Task<List<T>> GetListAsync<T>(HttpClient client, string token, string path)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<T>>(TestContext.Current.CancellationToken);

        return page.ShouldNotBeNull().Items.ToList();
    }

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
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

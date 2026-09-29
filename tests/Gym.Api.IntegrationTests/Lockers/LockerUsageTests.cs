using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Lockers.LockerUsage;
using Gym.Domain.Members;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Lockers;

/// <summary>
/// Visits per locker over the last 7, 30 or 90 days: BUSINESS_RULES.md §6 <i>Locker usage map</i>
/// (roadmap 6.5.15).
/// </summary>
/// <remarks>
/// The API stamps check-ins with the real clock, so the period tests write visits straight into the
/// table at chosen moments and run <see cref="LockerUsageHandler"/> against a
/// <see cref="FakeTimeProvider"/> pinned to 2026-09-30, 13:30 in Tehran. The endpoint tests go
/// through the API: roles, the accepted periods, a cancelled check-in and a moved visit.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LockerUsageTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>10:00 UTC is 13:30 in Tehran.</summary>
    private static readonly DateOnly Today = new(2026, 9, 30);

    private static int _phoneSuffix;

    [Fact]
    public async Task Handle_VisitsOnSomeLockers_CountsEachAndListsEveryLockerInOrder()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today, 9));
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-2), 18));
        await InsertVisitAsync(visit, lockerNumber: 40, At(Today.AddDays(-3), 7));

        var result = await HandleAsync(days: 7);

        result.Lockers.Select(locker => locker.Number).ShouldBe(Enumerable.Range(1, 72));
        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(2);
        result.Lockers.Single(locker => locker.Number == 40).Uses.ShouldBe(1);
        result.Lockers.Sum(locker => locker.Uses).ShouldBe(3);
    }

    [Fact]
    public async Task Handle_SevenDays_CoversTodayAndTheSixDaysBefore()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        // 00:00 on the first day of the period, on the gym's clock: counted.
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-6), 0));
        // 23:59 on the day before it: not counted, however close.
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-7), 23, 59));
        // Earlier today: counted.
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today, 8));

        var result = await HandleAsync(days: 7);

        result.From.ShouldBe(Today.AddDays(-6));
        result.To.ShouldBe(Today);
        result.Days.ShouldBe(7);
        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(2);
    }

    [Theory]
    [InlineData(30, 2)]
    [InlineData(90, 3)]
    public async Task Handle_LongerPeriod_ReachesFurtherBack(int days, int expectedUses)
    {
        var visit = await AddMemberWithSubscriptionAsync();
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-10), 12));
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-29), 12));
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-89), 12));
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today.AddDays(-90), 12));

        var result = await HandleAsync(days);

        result.From.ShouldBe(Today.AddDays(1 - days));
        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(expectedUses);
    }

    [Fact]
    public async Task Handle_CancelledCheckIn_IsNotCounted()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today, 9), cancelled: true);
        await InsertVisitAsync(visit, lockerNumber: 5, At(Today, 10));

        var result = await HandleAsync(days: 7);

        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(1);
    }

    [Theory]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Owner)]
    public async Task LockerUsage_EitherRoleAfterACheckIn_CountsItOnItsLocker(string role)
    {
        var (client, token) = await ClientAsync(role);
        var visit = await AddMemberWithSubscriptionAsync();
        await TestLockers.CheckInOkAsync(client, token, visit.MemberId, lockerNumber: 5);

        var result = await UsageOkAsync(client, token, days: 7);

        result.To.ShouldBe(RealToday());
        result.Lockers.Count.ShouldBe(72);
        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(1);
        result.Lockers.Sum(locker => locker.Uses).ShouldBe(1);
    }

    [Fact]
    public async Task LockerUsage_CheckInCancelled_IsNotCounted()
    {
        var (client, token) = await ClientAsync(Roles.Staff);
        var visit = await AddMemberWithSubscriptionAsync();
        var attendance = await TestLockers.CheckInOkAsync(client, token, visit.MemberId, lockerNumber: 5);
        using (var cancelled = await PostAsync(client, token, $"/api/attendance/{attendance.Id}/cancel", CancelCheckInBody.KeepPurchases))
        {
            cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var result = await UsageOkAsync(client, token, days: 7);

        result.Lockers.ShouldAllBe(locker => locker.Uses == 0);
    }

    [Fact]
    public async Task LockerUsage_VisitMovedToAnotherLocker_CountsForTheNewLockerOnly()
    {
        var (client, token) = await ClientAsync(Roles.Staff);
        var visit = await AddMemberWithSubscriptionAsync();
        var attendance = await TestLockers.CheckInOkAsync(client, token, visit.MemberId, lockerNumber: 5);
        using (var moved = await PostAsync(client, token, $"/api/attendance/{attendance.Id}/move-locker", new { lockerId = TestLockers.IdOf(7) }))
        {
            moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var result = await UsageOkAsync(client, token, days: 7);

        result.Lockers.Single(locker => locker.Number == 5).Uses.ShouldBe(0);
        result.Lockers.Single(locker => locker.Number == 7).Uses.ShouldBe(1);
    }

    [Fact]
    public async Task LockerUsage_NoDaysGiven_CoversThirtyDays()
    {
        var (client, token) = await ClientAsync(Roles.Staff);

        using var response = await UsageAsync(client, token, "/api/lockers/usage");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<LockerUsageResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        result.Days.ShouldBe(30);
        result.From.ShouldBe(RealToday().AddDays(-29));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(365)]
    public async Task LockerUsage_DaysNotOfferedByTheView_Returns400(int days)
    {
        var (client, token) = await ClientAsync(Roles.Owner);

        using var response = await UsageAsync(client, token, $"/api/lockers/usage?days={days}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LockerUsage_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync("/api/lockers/usage?days=7", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private sealed record MemberSubscription(Guid MemberId, Guid SubscriptionId);

    /// <summary>The handler under a clock pinned to 13:30 in Tehran on <see cref="Today"/>.</summary>
    private async Task<LockerUsageResponse> HandleAsync(int days)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new LockerUsageHandler(db, calendar).Handle(new LockerUsageQuery(days), TestContext.Current.CancellationToken);
    }

    private DateOnly RealToday()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private async Task<(HttpClient Client, string Token)> ClientAsync(string role)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "desk", role: role);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("desk", TestUsers.Password));
    }

    /// <summary>A member with a plan assigned through the API, so the visits written below have a subscription.</summary>
    private async Task<MemberSubscription> AddMemberWithSubscriptionAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98919{suffix:D7}");
        var plan = await TestPlans.AddAsync(Fixture);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "assigner", role: Roles.Owner);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("assigner", TestUsers.Password);
        using var assigned = await PostAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        var subscriptionId = await db.Subscriptions
            .Where(s => s.MemberId == member.Id)
            .Select(s => s.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        return new MemberSubscription(member.Id, subscriptionId);
    }

    /// <summary>
    /// A closed visit on a locker, written directly at a chosen moment. Closed, so any number of them
    /// can belong to the same member and locker; a cancelled one closes at the moment it was
    /// cancelled, as the table demands.
    /// </summary>
    private async Task InsertVisitAsync(MemberSubscription visit, int lockerNumber, DateTimeOffset checkedInAt, bool cancelled = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOutAt = checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {visit.MemberId}, {visit.SubscriptionId}, {TestLockers.IdOf(lockerNumber)}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> UsageAsync(HttpClient client, string token, string path) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token), TestContext.Current.CancellationToken);

    private static async Task<LockerUsageResponse> UsageOkAsync(HttpClient client, string token, int days)
    {
        using var response = await UsageAsync(client, token, $"/api/lockers/usage?days={days}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<LockerUsageResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}

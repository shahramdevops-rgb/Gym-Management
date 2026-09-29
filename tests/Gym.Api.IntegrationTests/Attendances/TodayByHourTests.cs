using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances.TodayByHour;
using Gym.Application.Common;
using Gym.Domain.Members;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// Today's check-ins by hour and the same weekday's 4-week average: BUSINESS_RULES.md §6 <i>Today
/// by hour</i> (roadmap 6.5.14).
/// </summary>
/// <remarks>
/// The API stamps check-ins with the real clock, so the counting tests write visits straight into
/// the table at chosen moments and run <see cref="TodayByHourHandler"/> against a
/// <see cref="FakeTimeProvider"/> pinned to Wednesday 2026-09-30, 13:30 in Tehran. The endpoint
/// tests then only check the wiring: roles, and a real check-in showing up.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TodayByHourTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>A Wednesday. 10:00 UTC is 13:30 in Tehran.</summary>
    private static readonly DateOnly Today = new(2026, 9, 30);

    private static int _phoneSuffix;

    [Fact]
    public async Task Handle_CheckInsAroundTheGymsMidnight_AreCountedByTheirHourInTheGymsZone()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        // 20:40 UTC on the 29th is 00:10 on the 30th in Tehran: today, hour 0.
        await InsertVisitAsync(visit, new DateTimeOffset(2026, 9, 29, 20, 40, 0, TimeSpan.Zero));
        // 20:20 UTC on the 29th is 23:50 on the 29th in Tehran: yesterday, not counted.
        await InsertVisitAsync(visit, new DateTimeOffset(2026, 9, 29, 20, 20, 0, TimeSpan.Zero));
        // 05:00 UTC is 08:30 in Tehran: hour 8, not hour 5.
        await InsertVisitAsync(visit, new DateTimeOffset(2026, 9, 30, 5, 0, 0, TimeSpan.Zero));

        var result = await HandleAsync();

        result.Date.ShouldBe(Today);
        result.Hours.Select(hour => hour.Hour).ShouldBe(Enumerable.Range(0, 24));
        result.Hours[0].Today.ShouldBe(1);
        result.Hours[8].Today.ShouldBe(1);
        result.Hours.Sum(hour => hour.Today).ShouldBe(2);
    }

    [Fact]
    public async Task Handle_CancelledCheckIns_AreNotCountedTodayOrInTheAverage()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        await InsertVisitAsync(visit, At(Today, 18));
        await InsertVisitAsync(visit, At(Today, 18, minute: 20), cancelled: true);
        await InsertVisitAsync(visit, At(Today.AddDays(-7), 18));
        await InsertVisitAsync(visit, At(Today.AddDays(-7), 18, minute: 20), cancelled: true);
        // A past day whose only check-in was cancelled was not an open day: it is left out.
        await InsertVisitAsync(visit, At(Today.AddDays(-14), 18), cancelled: true);

        var result = await HandleAsync();

        result.Hours[18].Today.ShouldBe(1);
        result.Hours[18].Average.ShouldBe(1);
        result.DaysAveraged.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_PreviousFourSameWeekdays_AveragesOnlyTheDaysWithCheckIns()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        // A week back: two at 18. Two weeks back: one at 18. Three weeks back: closed.
        // Four weeks back: one at 9 and none at 18.
        await InsertVisitAsync(visit, At(Today.AddDays(-7), 18));
        await InsertVisitAsync(visit, At(Today.AddDays(-7), 18, minute: 45));
        await InsertVisitAsync(visit, At(Today.AddDays(-14), 18, minute: 10));
        await InsertVisitAsync(visit, At(Today.AddDays(-28), 9));
        // Neither of these is one of the 4 days: five weeks back, and yesterday.
        await InsertVisitAsync(visit, At(Today.AddDays(-35), 18));
        await InsertVisitAsync(visit, At(Today.AddDays(-1), 18));

        var result = await HandleAsync();

        result.DaysAveraged.ShouldBe(3);
        result.Hours[18].Average.ShouldBe(1);   // (2 + 1 + 0) / 3
        result.Hours[9].Average.ShouldBe(0.3);  // (0 + 0 + 1) / 3, rounded
        result.Hours[10].Average.ShouldBe(0);
        result.Hours.Sum(hour => hour.Today).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_NoCheckInsOnThePreviousFourWeekdays_AveragesAreZero()
    {
        var visit = await AddMemberWithSubscriptionAsync();
        await InsertVisitAsync(visit, At(Today, 18));

        var result = await HandleAsync();

        result.DaysAveraged.ShouldBe(0);
        result.Hours.ShouldAllBe(hour => hour.Average == 0);
        result.Hours[18].Today.ShouldBe(1);
    }

    [Theory]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Owner)]
    public async Task TodayByHour_EitherRoleAfterACheckIn_CountsItForToday(string role)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "desk", role: role);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("desk", TestUsers.Password);
        var visit = await AddMemberWithSubscriptionAsync();
        await TestLockers.CheckInOkAsync(client, token, visit.MemberId, lockerNumber: 5);

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/attendance/today-by-hour").WithBearer(token),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<TodayByHourResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        result.Date.ShouldBe(RealToday());
        result.Hours.Count.ShouldBe(24);
        result.Hours.Sum(hour => hour.Today).ShouldBe(1);
    }

    [Fact]
    public async Task TodayByHour_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync("/api/attendance/today-by-hour", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private sealed record MemberSubscription(Guid MemberId, Guid SubscriptionId);

    /// <summary>The handler under a clock pinned to 13:30 in Tehran on <see cref="Today"/>.</summary>
    private async Task<TodayByHourResponse> HandleAsync()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new TodayByHourHandler(db, calendar).Handle(TestContext.Current.CancellationToken);
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

    /// <summary>A member with a plan assigned through the API, so the visits written below have a subscription.</summary>
    private async Task<MemberSubscription> AddMemberWithSubscriptionAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98918{suffix:D7}");
        var plan = await TestPlans.AddAsync(Fixture);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "assigner", role: Roles.Owner);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("assigner", TestUsers.Password);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{member.Id}/subscriptions")
        {
            Content = JsonContent.Create(plan.Body),
        };
        using var assigned = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        var subscriptionId = await db.Subscriptions
            .Where(s => s.MemberId == member.Id)
            .Select(s => s.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        return new MemberSubscription(member.Id, subscriptionId);
    }

    /// <summary>
    /// A closed visit written directly at a chosen moment. Closed, so any number of them can belong
    /// to the same member; a cancelled one closes at the moment it was cancelled, as the table demands.
    /// </summary>
    private async Task InsertVisitAsync(MemberSubscription visit, DateTimeOffset checkedInAt, bool cancelled = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOutAt = checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {visit.MemberId}, {visit.SubscriptionId}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }
}

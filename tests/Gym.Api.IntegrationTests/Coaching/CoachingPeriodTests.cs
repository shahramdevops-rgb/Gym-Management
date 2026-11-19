using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Domain.Audit;
using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Coaching;

/// <summary>
/// The coaching period (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7h): the days the
/// trainer types when accepting, «تمدید», the days left of a link made before 15.7h, and the fixed
/// schedule held inside the period.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CoachingPeriodTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string TrainerPhone = "+989351234567";
    private const string StudentsPath = "/api/site/coaching/students";
    private const string MyTrainerPath = "/api/site/coaching/my-trainer";

    // ---- Accepting with a number of days ----

    [Fact]
    public async Task Accept_DaysTyped_BothSidesSeeThePeriodsLastDay()
    {
        var gym = await GymAsync();
        var requestId = await RequestIdAsync(gym);

        using var accepted = await AcceptAsync(gym, requestId, new { days = 45 });

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var expected = Iso(Today().AddDays(44));
        (await accepted.ReadJsonAsync()).GetProperty("students").EnumerateArray().Single()
            .GetProperty("periodEndsOn").GetString().ShouldBe(expected);
        var mine = await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken);
        mine.GetProperty("trainer").GetProperty("periodEndsOn").GetString().ShouldBe(expected);
    }

    [Fact]
    public async Task Accept_NoBody_ThirtyDays()
    {
        // An app still on the page before 15.7h sends nothing.
        var gym = await GymAsync();
        var requestId = await RequestIdAsync(gym);

        using var accepted = await AcceptAsync(gym, requestId, body: null);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var period = (await PeriodsAsync()).ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((Today(), Today().AddDays(CoachingPeriod.DefaultDays - 1)));
    }

    [Theory]
    [InlineData(CoachingPeriod.MinDays - 1)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public async Task Accept_DaysOutOfRange_Returns400AndTheRequestStillWaits(int days)
    {
        var gym = await GymAsync();
        var requestId = await RequestIdAsync(gym);

        using var accepted = await AcceptAsync(gym, requestId, new { days });

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodDaysOutOfRange.Code);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TrainerRequests.SingleAsync(TestContext.Current.CancellationToken)).IsPending.ShouldBeTrue();
        (await db.TrainerLinks.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ---- The trainer's mark (15.7n) ----

    [Fact]
    public async Task Students_PeriodSoonDays_IsTheSmsSettingsNumberWhetherTheSmsIsOnOrOff()
    {
        var gym = await LinkedGymAsync(days: 30);

        var before = await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken);
        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "UPDATE sms_settings SET coaching_period_ending_days = 10", TestContext.Current.CancellationToken);
        }

        var after = await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken);

        before.GetProperty("periodSoonDays").GetInt32().ShouldBe(7);
        after.GetProperty("periodSoonDays").GetInt32().ShouldBe(10);
    }

    // ---- Renewing ----

    [Fact]
    public async Task Renew_BeforeTheEnd_StartsTheDayAfterTheLastDay()
    {
        var gym = await LinkedGymAsync(days: 30);

        using var renewed = await RenewAsync(gym, 20);

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await renewed.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(49)));
        var periods = (await PeriodsAsync()).OrderBy(period => period.StartsOn).ToList();
        periods.Count.ShouldBe(2);
        (periods[1].StartsOn, periods[1].EndsOn).ShouldBe((Today().AddDays(30), Today().AddDays(49)));
    }

    [Fact]
    public async Task Renew_FewerThanSevenDays_Returns400()
    {
        var gym = await LinkedGymAsync(days: 30);

        using var renewed = await RenewAsync(gym, CoachingPeriod.MinDays - 1);

        renewed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await renewed.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodDaysOutOfRange.Code);
        (await PeriodsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Renew_NotTheTrainersStudent_Returns404()
    {
        var gym = await GymAsync();

        using var renewed = await RenewAsync(gym, 30);

        renewed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await renewed.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.StudentNotFound.Code);
    }

    [Fact]
    public async Task Renew_AsTheMember_Returns403()
    {
        var gym = await LinkedGymAsync(days: 30);

        using var renewed = await RenewAsync(gym, 30, gym.MemberToken);

        renewed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- «ویرایش دوره»: the days left, earlier or later (15.7k) ----

    [Fact]
    public async Task SetDaysLeft_AfterRenewalsByMistake_RemovesThemAndCutsTheScheduleAndTheDatesAfter()
    {
        var gym = await LinkedGymAsync(days: 30);
        var today = Today();
        using (var renewed = await RenewAsync(gym, 30))
        {
            renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await AddOldSessionsAsync(gym, today);

        using var set = await SetDaysLeftAsync(gym, 3);

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await set.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(today.AddDays(2)));
        var period = (await PeriodsAsync()).ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((today, today.AddDays(2)));

        // As for the days left of a student from before: the schedule in force ends on the new last
        // day, the later one is gone, and so is the date moved to after it.
        (await SchedulesAsync()).ShouldHaveSingleItem().EndsOn.ShouldBe(today.AddDays(2));
        (await BookingsAsync()).ShouldHaveSingleItem().Date.ShouldBe(today.AddDays(1));
    }

    [Fact]
    public async Task SetDaysLeft_PastThePeriods84Days_SavedAsTwoPeriods()
    {
        var gym = await LinkedGymAsync(days: 30);
        await MovePeriodStartAsync(daysBack: 20);
        var today = Today();

        using var set = await SetDaysLeftAsync(gym, CoachingPeriod.MaxDays);

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await set.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(today.AddDays(CoachingPeriod.MaxDays - 1)));
        var periods = (await PeriodsAsync()).OrderBy(period => period.StartsOn).ToList();
        periods.Count.ShouldBe(2);
        periods[1].StartsOn.ShouldBe(periods[0].EndsOn.AddDays(1));
    }

    [Fact]
    public async Task SetDaysLeft_More_CutsNothing()
    {
        var gym = await LinkedGymAsync(days: 30);
        using (var saved = await SetScheduleAsync(gym, Today().AddDays(1), Today().AddDays(20)))
        {
            saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var set = await SetDaysLeftAsync(gym, 60);

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SchedulesAsync()).ShouldHaveSingleItem().EndsOn.ShouldBe(Today().AddDays(20));
    }

    [Fact]
    public async Task SetDaysLeft_PeriodOver_Returns409()
    {
        var gym = await LinkedGymAsync(days: 30);
        await MovePeriodStartAsync(daysBack: 40);

        using var set = await SetDaysLeftAsync(gym, 10);

        set.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await set.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodNotRunning.Code);
    }

    [Fact]
    public async Task SetDaysLeft_LinkWithoutPeriod_Returns409()
    {
        var gym = await LinkedGymAsync(days: 30);
        await RemovePeriodsAsync();

        using var set = await SetDaysLeftAsync(gym, 10);

        set.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await set.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodNotRunning.Code);
    }

    [Fact]
    public async Task SetDaysLeft_OutOfRange_Returns400AndChangesNothing()
    {
        var gym = await LinkedGymAsync(days: 30);

        using var set = await SetDaysLeftAsync(gym, CoachingPeriod.MaxDays + 1);

        set.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await set.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodDaysOutOfRange.Code);
        (await PeriodsAsync()).ShouldHaveSingleItem().EndsOn.ShouldBe(Today().AddDays(29));
    }

    [Fact]
    public async Task SetDaysLeft_NotTheTrainersStudent_Returns404()
    {
        var gym = await GymAsync();

        using var set = await SetDaysLeftAsync(gym, 10);

        set.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await set.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.StudentNotFound.Code);
    }

    [Fact]
    public async Task SetDaysLeft_AsTheMember_Returns403()
    {
        var gym = await LinkedGymAsync(days: 30);

        using var set = await SetDaysLeftAsync(gym, 10, gym.MemberToken);

        set.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetDaysLeft_Shortened_WrittenToTheAuditLog()
    {
        var gym = await LinkedGymAsync(days: 30);
        using (var renewed = await RenewAsync(gym, 30))
        {
            renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var set = await SetDaysLeftAsync(gym, 10);

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var scope = Fixture.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(row => row.EntityType == nameof(CoachingPeriod))
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.ShouldContain(row => row.Action == AuditAction.Delete);
        rows.ShouldContain(row => row.Action == AuditAction.Update);
    }

    // ---- The fixed schedule inside the period ----

    [Fact]
    public async Task SetSchedule_PastThePeriodsLastDay_Returns422()
    {
        var gym = await LinkedGymAsync(days: 10);

        using var saved = await SetScheduleAsync(gym, Today().AddDays(1), Today().AddDays(10));

        saved.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await saved.ReadErrorCodeAsync()).ShouldBe(BookingErrors.ScheduleAfterPeriod.Code);
        (await SchedulesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetSchedule_ToThePeriodsLastDay_Saved()
    {
        var gym = await LinkedGymAsync(days: 10);

        using var saved = await SetScheduleAsync(gym, Today().AddDays(1), Today().AddDays(9));

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await saved.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(9)));
    }

    // ---- A link made before 15.7h ----

    [Fact]
    public async Task Students_LinkWithoutPeriod_PeriodEndsOnIsNull()
    {
        var gym = await LinkedGymAsync(days: 30);
        await RemovePeriodsAsync();

        var students = await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken);

        students.GetProperty("students").EnumerateArray().Single().GetProperty("periodEndsOn").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetSchedule_LinkWithoutPeriod_KeepsTheTimetablesLimitOnly()
    {
        var gym = await LinkedGymAsync(days: 30);
        await RemovePeriodsAsync();

        using var saved = await SetScheduleAsync(gym, Today().AddDays(1), Today().AddDays(55));

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Renew_LinkWithoutPeriod_DaysLeftFromTodayCutTheScheduleAndTheDatesAfter()
    {
        var gym = await LinkedGymAsync(days: 30);
        await RemovePeriodsAsync();
        var today = Today();
        await AddOldSessionsAsync(gym, today);

        using var renewed = await RenewAsync(gym, 3);

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await renewed.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(today.AddDays(2)));
        var period = (await PeriodsAsync()).ShouldHaveSingleItem();
        (period.StartsOn, period.EndsOn).ShouldBe((today, today.AddDays(2)));

        // The schedule in force ends on the period's last day, the later one is gone, and so is the
        // date moved to after it; the date moved to inside the period stays.
        var schedule = (await SchedulesAsync()).ShouldHaveSingleItem();
        schedule.EndsOn.ShouldBe(today.AddDays(2));
        var booking = (await BookingsAsync()).ShouldHaveSingleItem();
        booking.Date.ShouldBe(today.AddDays(1));
    }

    [Fact]
    public async Task Renew_LinkWithoutPeriodOneDayLeft_Allowed()
    {
        var gym = await LinkedGymAsync(days: 30);
        await RemovePeriodsAsync();

        using var renewed = await RenewAsync(gym, CoachingPeriod.MinDaysLeft);

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await renewed.ReadJsonAsync()).GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today()));
    }

    // ---- The table holds it too ----

    [Fact]
    public async Task Insert_OverlappingPeriodsOfALink_RejectedByTheExclusionConstraint()
    {
        await LinkedGymAsync(days: 30);
        var linkId = (await PeriodsAsync()).Single().LinkId;
        var today = Today();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertPeriodAsync(linkId, today.AddDays(29), today.AddDays(40)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);
        exception.ConstraintName.ShouldBe("ex_coaching_periods_link_id_dates");
    }

    [Fact]
    public async Task Insert_PeriodRightAfterTheLastDay_Allowed()
    {
        await LinkedGymAsync(days: 30);
        var linkId = (await PeriodsAsync()).Single().LinkId;
        var today = Today();

        await InsertPeriodAsync(linkId, today.AddDays(30), today.AddDays(40));

        (await PeriodsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Insert_PeriodLongerThan84Days_RejectedByTheCheckConstraint()
    {
        await LinkedGymAsync(days: 30);
        var linkId = (await PeriodsAsync()).Single().LinkId;
        var today = Today();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertPeriodAsync(linkId, today.AddDays(30), today.AddDays(30 + 84)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_coaching_periods_at_most_84_days");
    }

    // ---- Helpers ----

    /// <summary>A trainer with a weekly session every day at 13:00 from tomorrow, and a member; both signed in, not linked.</summary>
    private async Task<Gym> GymAsync()
    {
        var member = await AddMemberAsync();
        await AddTrainerAsync();
        var client = Fixture.CreateClient();
        var gym = new Gym(client, await SignInAsync(client, TrainerPhone), await SignInAsync(client, MemberPhone), member.Id);

        var everyDay = Enum.GetValues<DayOfWeek>()
            .Select(day => new { day = day.ToString(), start = "13:00:00", end = "14:00:00", capacity = 5 })
            .ToArray();
        using var pattern = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", new
        {
            startsOn = Iso(Today().AddDays(1)),
            sessions = everyDay,
        });
        pattern.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    private async Task<Gym> LinkedGymAsync(int days)
    {
        var gym = await GymAsync();
        using var accepted = await AcceptAsync(gym, await RequestIdAsync(gym), new { days });
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    private static async Task<Guid> RequestIdAsync(Gym gym)
    {
        using var asked = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/coaching/my-trainer/request", new { trainerId = await TrainerIdAsync(gym) }, gym.MemberToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await asked.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
    }

    private static async Task<Guid> TrainerIdAsync(Gym gym)
    {
        var trainers = await GetJsonAsync(gym.Client, "/api/site/coaching/trainers", gym.MemberToken);

        return trainers.EnumerateArray().Single().GetProperty("trainerId").GetGuid();
    }

    private static Task<HttpResponseMessage> AcceptAsync(Gym gym, Guid requestId, object? body)
    {
        var request = Send(HttpMethod.Post, $"/api/site/coaching/requests/{requestId}/accept", gym.TrainerToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return gym.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> RenewAsync(Gym gym, int days, string? token = null) =>
        SendJsonAsync(gym, HttpMethod.Post, $"{StudentsPath}/{gym.MemberId}/period", new { days }, token);

    private static Task<HttpResponseMessage> SetDaysLeftAsync(Gym gym, int daysLeft, string? token = null) =>
        SendJsonAsync(gym, HttpMethod.Post, $"{StudentsPath}/{gym.MemberId}/period/days-left", new { daysLeft }, token);

    /// <summary>Moves the link's only period <paramref name="daysBack"/> days back, as if accepted then.</summary>
    private async Task MovePeriodStartAsync(int daysBack)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE coaching_periods SET starts_on = starts_on - {daysBack}, ends_on = ends_on - {daysBack}",
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> SetScheduleAsync(Gym gym, DateOnly startsOn, DateOnly endsOn) =>
        SendJsonAsync(gym, HttpMethod.Put, $"{StudentsPath}/{gym.MemberId}/schedule", new
        {
            startsOn = Iso(startsOn),
            endsOn = Iso(endsOn),
            slots = new[] { new { day = startsOn.DayOfWeek.ToString(), start = "13:00:00", end = "14:00:00" } },
        });

    /// <summary>
    /// What a link made before 15.7h may hold: a schedule from today for eight weeks, a later one,
    /// and dates moved to tomorrow and ten days on. Written straight to the database, as the API
    /// would now refuse them past the period.
    /// </summary>
    private async Task AddOldSessionsAsync(Gym gym, DateOnly today)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await db.TrainerLinks.SingleAsync(TestContext.Current.CancellationToken);
        WeeklySlot[] slots = [new(DayOfWeek.Saturday, new TimeOnly(13, 0), new TimeOnly(14, 0))];

        db.FixedSchedules.Add(FixedSchedule.Create(link.TrainerId, gym.MemberId, link.Id, today, today.AddDays(30), slots, today).Value);
        db.FixedSchedules.Add(FixedSchedule.Create(link.TrainerId, gym.MemberId, link.Id, today.AddDays(31), today.AddDays(55), slots, today).Value);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO session_bookings (id, trainer_id, member_id, link_id, date, start_time, end_time, booked_by, created_at)
            VALUES ({Guid.CreateVersion7()}, {link.TrainerId}, {gym.MemberId}, {link.Id}, {today.AddDays(1)}, {new TimeOnly(13, 0)}, {new TimeOnly(14, 0)}, 'Member', now()),
                   ({Guid.CreateVersion7()}, {link.TrainerId}, {gym.MemberId}, {link.Id}, {today.AddDays(10)}, {new TimeOnly(13, 0)}, {new TimeOnly(14, 0)}, 'Member', now())
            """,
            TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Makes the link one from before 15.7h: no period row.</summary>
    private async Task RemovePeriodsAsync()
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .ExecuteSqlAsync($"DELETE FROM coaching_periods", TestContext.Current.CancellationToken);
    }

    private async Task InsertPeriodAsync(Guid linkId, DateOnly startsOn, DateOnly endsOn)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"INSERT INTO coaching_periods (id, link_id, starts_on, ends_on, created_at) VALUES ({Guid.CreateVersion7()}, {linkId}, {startsOn}, {endsOn}, now())",
            TestContext.Current.CancellationToken);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static HttpRequestMessage Send(HttpMethod method, string path, string token) => new HttpRequestMessage(method, path).WithBearer(token);

    /// <summary>Sends <paramref name="body"/> as the trainer, or as <paramref name="token"/>'s account.</summary>
    private static Task<HttpResponseMessage> SendJsonAsync(Gym gym, HttpMethod method, string path, object body, string? token = null)
    {
        var request = Send(method, path, token ?? gym.TrainerToken);
        request.Content = JsonContent.Create(body);

        return gym.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path, string token)
    {
        using var response = await client.SendAsync(Send(HttpMethod.Get, path, token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadJsonAsync();
    }

    private async Task<string> SignInAsync(HttpClient client, string phone)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("علی کریمی", MemberPhone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task AddTrainerAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Trainers.Add(Trainer.Create("رضا محمدی", TrainerPhone, new DateOnly(2026, 10, 1)).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<CoachingPeriod>> PeriodsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CoachingPeriods.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<FixedSchedule>> SchedulesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().FixedSchedules.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SessionBooking>> BookingsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionBookings.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Gym(HttpClient Client, string TrainerToken, string MemberToken, Guid MemberId);
}

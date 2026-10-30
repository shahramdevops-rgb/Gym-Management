using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Application.Trainers.StopTrainer;
using Gym.Domain.Coaching;
using Gym.Domain.Timetables;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Timetables;

/// <summary>
/// The trainer's «برنامه سانس‌ها» on the site (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>,
/// task 15.6a): a weekly pattern from a date, one date changed on its own and kept through later
/// patterns, and the twelve weeks worked out from them.
/// </summary>
/// <remarks>
/// The clock here is the real one, so every date is tomorrow or later, where the time of day plays
/// no part; today's started sessions are the Domain tests'.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TimetableTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string MemberPhone = "+989121234567";

    private const string TimetablePath = "/api/site/timetable";
    private const string PatternPath = "/api/site/timetable/pattern";

    // ---- Reading ----

    [Fact]
    public async Task Get_BeforeAnyPattern_TwelveWeeksOfDaysWithNoSessions()
    {
        var (client, token) = await TrainerClientAsync();

        var body = await GetTimetableAsync(client, token);

        var today = Today();
        body.GetProperty("today").GetString().ShouldBe(Iso(today));
        body.GetProperty("lastDay").GetString().ShouldBe(Iso(today.AddDays(83)));
        body.GetProperty("pattern").ValueKind.ShouldBe(JsonValueKind.Null);
        var days = body.GetProperty("days").EnumerateArray().ToList();
        days.Count.ShouldBe(TimetableRules.DaysAhead);
        days[0].GetProperty("date").GetString().ShouldBe(Iso(today));
        days.ShouldAllBe(day => day.GetProperty("sessions").GetArrayLength() == 0 && !day.GetProperty("changedByHand").GetBoolean());
    }

    // ---- The weekly pattern ----

    [Fact]
    public async Task SetPattern_FromTomorrow_EachWeekdayGetsItsOwnSessionsEveryWeek()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);

        using var response = await SetPatternAsync(client, token, tomorrow,
            Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8),
            Weekly(DayOfWeek.Saturday, "14:00", "15:00", 6),
            Weekly(DayOfWeek.Sunday, "09:00", "10:30", 4));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        var pattern = body.GetProperty("pattern");
        pattern.GetProperty("startsOn").GetString().ShouldBe(Iso(tomorrow));
        pattern.GetProperty("sessions").GetArrayLength().ShouldBe(3);
        pattern.GetProperty("sessions")[0].GetProperty("day").GetString().ShouldBe("Sunday");

        var saturday = Next(DayOfWeek.Saturday, tomorrow);
        Sessions(body, saturday).ShouldBe(["13:00:00-14:00:00/8", "14:00:00-15:00:00/6"]);
        Sessions(body, saturday.AddDays(7)).ShouldBe(["13:00:00-14:00:00/8", "14:00:00-15:00:00/6"]);
        Sessions(body, Next(DayOfWeek.Sunday, tomorrow)).ShouldBe(["09:00:00-10:30:00/4"]);
        Sessions(body, Next(DayOfWeek.Monday, tomorrow)).ShouldBeEmpty();
        Sessions(body, Today()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPattern_OverlappingSessions_Succeeds()
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetPatternAsync(client, token, Today().AddDays(1),
            Weekly(DayOfWeek.Saturday, "13:00", "15:00", 8),
            Weekly(DayOfWeek.Saturday, "14:45", "17:00", 8));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetPattern_FromToday_Returns422()
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetPatternAsync(client, token, Today(), Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.PatternStartOutOfRange.Code);
        (await PatternsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("13:00", "14:00", 0, "Timetable.CapacityOutOfRange")]
    [InlineData("13:03", "14:00", 5, "Timetable.TimeNotOnStep")]
    [InlineData("14:00", "13:00", 5, "Timetable.EndNotAfterStart")]
    public async Task SetPattern_ABadSession_Returns400WithItsCode(string start, string end, int capacity, string code)
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetPatternAsync(client, token, Today().AddDays(1), Weekly(DayOfWeek.Saturday, start, end, capacity));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(code);
    }

    [Fact]
    public async Task SetPattern_FromALaterDate_TheEarlierOneStaysUntilThen()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);
        var firstSaturday = Next(DayOfWeek.Saturday, tomorrow);
        using var first = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        using var second = await SetPatternAsync(client, token, firstSaturday.AddDays(1), Weekly(DayOfWeek.Saturday, "17:00", "18:00", 5));

        var body = await second.ReadJsonAsync();
        Sessions(body, firstSaturday).ShouldBe(["13:00:00-14:00:00/8"]);
        Sessions(body, firstSaturday.AddDays(7)).ShouldBe(["17:00:00-18:00:00/5"]);
        body.GetProperty("pattern").GetProperty("startsOn").GetString().ShouldBe(Iso(firstSaturday.AddDays(1)));
        (await PatternsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetPattern_FromAnEarlierDate_ReplacesTheOneSavedFromLater()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);
        using var later = await SetPatternAsync(client, token, tomorrow.AddDays(10), Weekly(DayOfWeek.Saturday, "17:00", "18:00", 5));

        using var earlier = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        earlier.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await earlier.ReadJsonAsync();
        Sessions(body, Next(DayOfWeek.Saturday, tomorrow.AddDays(10))).ShouldBe(["13:00:00-14:00:00/8"]);
        (await PatternsAsync()).ShouldHaveSingleItem().StartsOn.ShouldBe(tomorrow);
    }

    [Fact]
    public async Task SetPattern_TwiceFromTheSameDateAtOnce_LeavesOnePattern()
    {
        var (client, token) = await TrainerClientAsync();
        using var otherPhone = Fixture.CreateClient();
        var tomorrow = Today().AddDays(1);

        var responses = await Task.WhenAll(
            SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8)),
            SetPatternAsync(otherPhone, token, tomorrow, Weekly(DayOfWeek.Saturday, "17:00", "18:00", 5)));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        (await PatternsAsync()).ShouldHaveSingleItem().Sessions.Count.ShouldBe(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    // ---- One date on its own ----

    [Fact]
    public async Task SetDay_ChangesThatDateOnly_AndKeepsItThroughALaterPattern()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);
        var saturday = Next(DayOfWeek.Saturday, tomorrow);
        using var pattern = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        using var changed = await SetDayAsync(client, token, saturday, Session("16:00", "17:30", 3));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await changed.ReadJsonAsync();
        Sessions(body, saturday).ShouldBe(["16:00:00-17:30:00/3"]);
        Day(body, saturday).GetProperty("changedByHand").GetBoolean().ShouldBeTrue();
        Sessions(body, saturday.AddDays(7)).ShouldBe(["13:00:00-14:00:00/8"]);

        using var newPattern = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "09:00", "10:00", 5));

        var after = await newPattern.ReadJsonAsync();
        Sessions(after, saturday).ShouldBe(["16:00:00-17:30:00/3"]);
        Sessions(after, saturday.AddDays(7)).ShouldBe(["09:00:00-10:00:00/5"]);
    }

    [Fact]
    public async Task SetDay_WithNoSessions_ClosesTheDay()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);
        var saturday = Next(DayOfWeek.Saturday, tomorrow);
        using var pattern = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        using var closed = await SetDayAsync(client, token, saturday);

        var body = await closed.ReadJsonAsync();
        Sessions(body, saturday).ShouldBeEmpty();
        Day(body, saturday).GetProperty("changedByHand").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task SetDay_AgainOnTheSameDate_ReplacesItsSessions()
    {
        var (client, token) = await TrainerClientAsync();
        var date = Today().AddDays(3);
        using var first = await SetDayAsync(client, token, date, Session("13:00", "14:00", 8), Session("15:00", "16:00", 8));

        using var second = await SetDayAsync(client, token, date, Session("13:00", "14:00", 10));

        Sessions(await second.ReadJsonAsync(), date).ShouldBe(["13:00:00-14:00:00/10"]);
        (await ChangedDaysAsync()).ShouldHaveSingleItem().Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SetDay_Yesterday_Returns422()
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetDayAsync(client, token, Today().AddDays(-1), Session("13:00", "14:00", 8));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.DayOutOfRange.Code);
    }

    [Fact]
    public async Task SetDay_PastTheLastDay_Returns422()
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetDayAsync(client, token, Today().AddDays(TimetableRules.DaysAhead), Session("13:00", "14:00", 8));

        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.DayOutOfRange.Code);
    }

    [Fact]
    public async Task SetDay_SameSessionTwice_Returns400()
    {
        var (client, token) = await TrainerClientAsync();

        using var response = await SetDayAsync(client, token, Today().AddDays(2), Session("13:00", "14:00", 8), Session("13:00", "14:00", 4));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.DuplicateSession.Code);
    }

    [Fact]
    public async Task ResetDay_AChangedDate_TakesThePatternsSessionsAgain()
    {
        var (client, token) = await TrainerClientAsync();
        var tomorrow = Today().AddDays(1);
        var saturday = Next(DayOfWeek.Saturday, tomorrow);
        using var pattern = await SetPatternAsync(client, token, tomorrow, Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));
        using var changed = await SetDayAsync(client, token, saturday);

        using var reset = await client.SendAsync(Send(HttpMethod.Delete, DayPath(saturday), token), TestContext.Current.CancellationToken);

        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await reset.ReadJsonAsync();
        Sessions(body, saturday).ShouldBe(["13:00:00-14:00:00/8"]);
        Day(body, saturday).GetProperty("changedByHand").GetBoolean().ShouldBeFalse();
        (await ChangedDaysAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetDay_ADateNeverChanged_AnswersTheTimetableAsItIs()
    {
        var (client, token) = await TrainerClientAsync();

        using var reset = await client.SendAsync(Send(HttpMethod.Delete, DayPath(Today().AddDays(2)), token), TestContext.Current.CancellationToken);

        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- Who may ask ----

    [Fact]
    public async Task Get_AMemberWhoIsNotATrainer_Returns403()
    {
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(TestMembers.Seed("علی کریمی", MemberPhone));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Send(HttpMethod.Get, TimetablePath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPattern_TrainerStoppedAfterSigningIn_Returns403AndSavesNothing()
    {
        // The token still says trainer for up to 15 minutes; the handler reads the Owner's stop now.
        var (client, token) = await TrainerClientAsync();
        await using (var scope = Fixture.CreateScope())
        {
            var trainer = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Trainers
                .SingleAsync(row => row.PhoneNumber == TrainerPhone, TestContext.Current.CancellationToken);
            (await scope.ServiceProvider.GetRequiredService<StopTrainerHandler>().Handle(trainer.Id, TestContext.Current.CancellationToken))
                .IsSuccess.ShouldBeTrue();
        }

        using var response = await SetPatternAsync(client, token, Today().AddDays(1), Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NotATrainer.Code);
        (await PatternsAsync()).ShouldBeEmpty();
    }

    // ---- The database's own checks ----

    [Fact]
    public async Task Database_ASessionOverTheCapacityLimit_IsRefused()
    {
        var (client, token) = await TrainerClientAsync();
        using var changed = await SetDayAsync(client, token, Today().AddDays(2), Session("13:00", "14:00", 8));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = () => db.Database.ExecuteSqlAsync($"UPDATE timetable_day_sessions SET capacity = 51", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<Npgsql.PostgresException>(update)).ConstraintName.ShouldBe("ck_timetable_day_sessions_capacity");
    }

    [Fact]
    public async Task Database_ATimeOffTheStep_IsRefused()
    {
        var (client, token) = await TrainerClientAsync();
        using var pattern = await SetPatternAsync(client, token, Today().AddDays(1), Weekly(DayOfWeek.Saturday, "13:00", "14:00", 8));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = () => db.Database.ExecuteSqlAsync($"UPDATE week_pattern_sessions SET start_time = '13:02'", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<Npgsql.PostgresException>(update)).ConstraintName.ShouldBe("ck_week_pattern_sessions_start_time_on_step");
    }

    // ---- Helpers ----

    private static object Weekly(DayOfWeek day, string start, string end, int capacity) =>
        new { day = day.ToString(), start = $"{start}:00", end = $"{end}:00", capacity };

    private static object Session(string start, string end, int capacity) =>
        new { start = $"{start}:00", end = $"{end}:00", capacity };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string DayPath(DateOnly date) => $"{TimetablePath}/days/{Iso(date)}";

    /// <summary>The first <paramref name="day"/> on or after <paramref name="from"/>.</summary>
    private static DateOnly Next(DayOfWeek day, DateOnly from) => from.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);

    private static JsonElement Day(JsonElement timetable, DateOnly date) =>
        timetable.GetProperty("days").EnumerateArray().Single(day => day.GetProperty("date").GetString() == Iso(date));

    /// <summary>A date's sessions as "start-end/capacity", in the response's order.</summary>
    private static List<string> Sessions(JsonElement timetable, DateOnly date) =>
        Day(timetable, date).GetProperty("sessions").EnumerateArray()
            .Select(session => $"{session.GetProperty("start").GetString()}-{session.GetProperty("end").GetString()}/{session.GetProperty("capacity").GetInt32()}")
            .ToList();

    private static HttpRequestMessage Send(HttpMethod method, string path, string token) => new HttpRequestMessage(method, path).WithBearer(token);

    private static Task<HttpResponseMessage> SetPatternAsync(HttpClient client, string token, DateOnly startsOn, params object[] sessions)
    {
        var request = Send(HttpMethod.Put, PatternPath, token);
        request.Content = JsonContent.Create(new { startsOn = Iso(startsOn), sessions });

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> SetDayAsync(HttpClient client, string token, DateOnly date, params object[] sessions)
    {
        var request = Send(HttpMethod.Put, DayPath(date), token);
        request.Content = JsonContent.Create(new { sessions });

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> GetTimetableAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(Send(HttpMethod.Get, TimetablePath, token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadJsonAsync();
    }

    /// <summary>A trainer now, signed in on the site.</summary>
    private async Task<(HttpClient Client, string Token)> TrainerClientAsync()
    {
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Trainers.Add(Trainer.Create("رضا محمدی", TrainerPhone, new DateOnly(2026, 10, 1)).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = Fixture.CreateClient();

        return (client, await SignInAsync(client, TrainerPhone));
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

    private async Task<List<WeekPattern>> PatternsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().WeekPatterns.AsNoTracking()
            .Include(pattern => pattern.Sessions)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<TimetableDay>> ChangedDaysAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TimetableDays.AsNoTracking()
            .Include(day => day.Sessions)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}

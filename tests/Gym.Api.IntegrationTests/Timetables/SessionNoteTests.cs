using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Members;
using Gym.Domain.Timetables;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Timetables;

/// <summary>
/// The trainer's notes on sessions (BUSINESS_RULES.md §15 <i>Session notes</i>, task 15.7e): the
/// standing note saved with the timetable, and the note on one date's session, seen by the trainer
/// and by every student of that session, those on leave too.
/// </summary>
/// <remarks>
/// The clock here is the real one, so the notes written through the API are on tomorrow or later;
/// those on a day that has passed are written straight to the database.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SessionNoteTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string MemberPhone = "+989121234567";

    private const string MySessionsPath = "/api/site/me/sessions";

    // ---- The standing note ----

    [Fact]
    public async Task SetPattern_ANotedSession_ShowsTheNoteInThePatternOnEveryDateAndToTheStudent()
    {
        var gym = await GymAsync(saturdayNote: "  مخصوص مبتدی‌ها  ");
        await ScheduleSaturdaysAsync(gym);

        var timetable = await GetJsonAsync(gym, "/api/site/timetable");
        var weekly = timetable.GetProperty("pattern").GetProperty("sessions").EnumerateArray()
            .Single(session => session.GetProperty("day").GetString() == "Saturday" && session.GetProperty("start").GetString() == "13:00:00");
        weekly.GetProperty("note").GetString().ShouldBe("مخصوص مبتدی‌ها");
        DaySession(timetable, gym.Saturday, "13:00:00").GetProperty("note").GetString().ShouldBe("مخصوص مبتدی‌ها");
        DaySession(timetable, gym.Saturday.AddDays(7), "13:00:00").GetProperty("note").GetString().ShouldBe("مخصوص مبتدی‌ها");
        DaySession(timetable, gym.Saturday, "17:00:00").GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);

        var mine = await GetJsonAsync(gym, MySessionsPath, gym.MemberToken);
        mine.GetProperty("sessions").EnumerateArray().ShouldAllBe(place => place.GetProperty("note").GetString() == "مخصوص مبتدی‌ها");
    }

    [Fact]
    public async Task SetPattern_ANoteOverTheLimit_Returns400AndSavesNothing()
    {
        var gym = await GymAsync();

        using var response = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", new
        {
            startsOn = Iso(gym.Tomorrow.AddDays(1)),
            sessions = new[] { Weekly(DayOfWeek.Saturday, 13, 2, new string('ن', SessionNoteText.MaxStandingLength + 1)) },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.NoteTooLong.Code);
        (await PatternNotesAsync()).ShouldAllBe(note => note == null);
    }

    [Fact]
    public async Task SetDay_ANotedSession_KeepsTheNoteOnThatDateOnly()
    {
        var gym = await GymAsync();

        using var changed = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(gym.Saturday)}", new
        {
            sessions = new[] { new { start = "13:00:00", end = "14:00:00", capacity = 2, note = "فقط بانوان" } },
        });

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var timetable = await changed.ReadJsonAsync();
        DaySession(timetable, gym.Saturday, "13:00:00").GetProperty("note").GetString().ShouldBe("فقط بانوان");
        DaySession(timetable, gym.Saturday.AddDays(7), "13:00:00").GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetPattern_ChangingOnlyTheNote_CancelsNoStudentsSession()
    {
        var gym = await GymAsync();
        await ScheduleSaturdaysAsync(gym);

        using var check = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/timetable/pattern/check", PatternBody(gym.Tomorrow, saturdayNote: "حوله بیاورید"));
        using var saved = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", PatternBody(gym.Tomorrow, saturdayNote: "حوله بیاورید"));

        check.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await check.ReadJsonAsync()).GetProperty("students").GetInt32().ShouldBe(0);
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var mine = await GetJsonAsync(gym, MySessionsPath, gym.MemberToken);
        mine.GetProperty("sessions").EnumerateArray().ShouldAllBe(place => !place.GetProperty("removed").GetBoolean());
        (await CancellationsAsync()).ShouldBe(0);
    }

    // ---- The note on one date ----

    [Fact]
    public async Task SetSessionNote_ASessionOfTheDate_ShowsItOnThatDateToTheTrainerAndTheStudent()
    {
        var gym = await GymAsync();
        await ScheduleSaturdaysAsync(gym);

        using var response = await SetNoteAsync(gym, gym.Saturday, 13, "  فردا تمرین پا است  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await response.ReadJsonAsync();
        RosterSession(roster, "13:00:00").GetProperty("dateNote").GetString().ShouldBe("فردا تمرین پا است");
        RosterSession(roster, "17:00:00").GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);

        var timetable = await GetJsonAsync(gym, "/api/site/timetable");
        Day(timetable, gym.Saturday).GetProperty("changedByHand").GetBoolean().ShouldBeFalse();
        DaySession(timetable, gym.Saturday, "13:00:00").GetProperty("dateNote").GetString().ShouldBe("فردا تمرین پا است");
        DaySession(timetable, gym.Saturday.AddDays(7), "13:00:00").GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);

        var mine = (await GetJsonAsync(gym, MySessionsPath, gym.MemberToken)).GetProperty("sessions");
        Place(mine, gym.Saturday).GetProperty("dateNote").GetString().ShouldBe("فردا تمرین پا است");
        Place(mine, gym.Saturday.AddDays(7)).GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);

        var schedule = (await GetJsonAsync(gym, $"/api/site/coaching/students/{gym.MemberId}/schedule")).GetProperty("sessions");
        Place(schedule, gym.Saturday).GetProperty("dateNote").GetString().ShouldBe("فردا تمرین پا است");
    }

    [Fact]
    public async Task SetSessionNote_Again_ChangesTheTextOfTheSameNote()
    {
        var gym = await GymAsync();
        using var first = await SetNoteAsync(gym, gym.Saturday, 13, "اول");

        using var second = await SetNoteAsync(gym, gym.Saturday, 13, "دوم");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterSession(await second.ReadJsonAsync(), "13:00:00").GetProperty("dateNote").GetString().ShouldBe("دوم");
        (await NotesAsync()).ShouldHaveSingleItem().Text.ShouldBe("دوم");
    }

    [Fact]
    public async Task SetSessionNote_ASessionTheDateDoesNotHave_Returns422()
    {
        var gym = await GymAsync();

        using var response = await SetNoteAsync(gym, gym.Saturday, 15, "یادداشت");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NoSuchSession.Code);
        (await NotesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetSessionNote_EndNotAfterStart_Returns422()
    {
        var gym = await GymAsync();

        using var response = await SendJsonAsync(gym, HttpMethod.Put, NotePath(gym.Saturday), new { start = "14:00:00", end = "13:00:00", text = "یادداشت" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NoSuchSession.Code);
        (await NotesAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetSessionNote_BlankText_Returns400(string text)
    {
        var gym = await GymAsync();

        using var response = await SetNoteAsync(gym, gym.Saturday, 13, text);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.NoteEmpty.Code);
    }

    [Fact]
    public async Task SetSessionNote_TextOverTheLimit_Returns400()
    {
        var gym = await GymAsync();

        using var response = await SetNoteAsync(gym, gym.Saturday, 13, new string('ن', SessionNoteText.MaxDateLength + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.DateNoteTooLong.Code);
    }

    [Fact]
    public async Task SetSessionNote_ADayThatHasPassed_Returns422()
    {
        var gym = await GymAsync();
        var yesterday = await PastDayAsync(gym, note: null);

        using var response = await SetNoteAsync(gym, yesterday, 13, "یادداشت");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.NoteSessionEnded.Code);
    }

    [Fact]
    public async Task SetSessionNote_AsAMember_Returns403()
    {
        var gym = await GymAsync();

        using var response = await SendJsonAsync(gym, HttpMethod.Put, NotePath(gym.Saturday), NoteBody(13, "یادداشت"), gym.MemberToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await NotesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveSessionNote_ANote_RemovesIt()
    {
        var gym = await GymAsync();
        using var written = await SetNoteAsync(gym, gym.Saturday, 13, "یادداشت");

        using var removed = await SendJsonAsync(gym, HttpMethod.Post, $"{NotePath(gym.Saturday)}/remove", Times(13));

        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        RosterSession(await removed.ReadJsonAsync(), "13:00:00").GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);
        (await NotesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveSessionNote_NoNote_Returns404()
    {
        var gym = await GymAsync();

        using var removed = await SendJsonAsync(gym, HttpMethod.Post, $"{NotePath(gym.Saturday)}/remove", Times(13));

        removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await removed.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.NoteNotFound.Code);
    }

    [Fact]
    public async Task RemoveSessionNote_ADayThatHasPassed_Returns422AndKeepsIt()
    {
        var gym = await GymAsync();
        var yesterday = await PastDayAsync(gym, note: "حوله بیاورید");

        using var removed = await SendJsonAsync(gym, HttpMethod.Post, $"{NotePath(yesterday)}/remove", Times(13));

        removed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await removed.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.NoteSessionEnded.Code);
        (await NotesAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SessionNote_TheSessionRemoved_IsNoLongerShownAndComesBackWithIt()
    {
        var gym = await GymAsync();
        using var written = await SetNoteAsync(gym, gym.Saturday, 13, "یادداشت");

        using var closed = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(gym.Saturday)}", new { sessions = Array.Empty<object>() });
        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var whileClosed = await GetJsonAsync(gym, StudentsPath(gym.Saturday));

        using var back = await gym.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/site/timetable/days/{Iso(gym.Saturday)}").WithBearer(gym.TrainerToken),
            TestContext.Current.CancellationToken);
        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterReset = await GetJsonAsync(gym, StudentsPath(gym.Saturday));

        whileClosed.GetProperty("sessions").GetArrayLength().ShouldBe(0);
        RosterSession(afterReset, "13:00:00").GetProperty("dateNote").GetString().ShouldBe("یادداشت");
    }

    [Fact]
    public async Task SessionNote_AStudentOnLeave_StillSeesIt()
    {
        var gym = await GymAsync();
        await ScheduleSaturdaysAsync(gym);
        using var written = await SetNoteAsync(gym, gym.Saturday, 13, "فردا تمرین پا است");

        await LeaveAcceptedAsync(gym, gym.Saturday, 13);

        var left = Place((await GetJsonAsync(gym, MySessionsPath, gym.MemberToken)).GetProperty("sessions"), gym.Saturday);
        left.GetProperty("left").GetBoolean().ShouldBeTrue();
        left.GetProperty("dateNote").GetString().ShouldBe("فردا تمرین پا است");
    }

    [Fact]
    public async Task SessionNote_ACancelledSessionWithItsStudent_ShowsNoNote()
    {
        var gym = await GymAsync(saturdayNote: "مخصوص مبتدی‌ها");
        await ScheduleSaturdaysAsync(gym);
        using var written = await SetNoteAsync(gym, gym.Saturday, 13, "فردا تمرین پا است");

        using var closed = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(gym.Saturday)}", new { sessions = Array.Empty<object>() });
        closed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelled = Place((await GetJsonAsync(gym, MySessionsPath, gym.MemberToken)).GetProperty("sessions"), gym.Saturday);
        cancelled.GetProperty("removed").GetBoolean().ShouldBeTrue();
        cancelled.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
        cancelled.GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);

        var roster = RosterSession(await GetJsonAsync(gym, StudentsPath(gym.Saturday)), "13:00:00");
        roster.GetProperty("removed").GetBoolean().ShouldBeTrue();
        roster.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
        roster.GetProperty("dateNote").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task TrainerDay_ADayThatHasPassed_ShowsBothNotes()
    {
        var gym = await GymAsync();
        var yesterday = await PastDayAsync(gym, note: "حوله بیاورید");

        var day = await GetJsonAsync(gym, $"/api/site/coaching/days/{Iso(yesterday)}");

        var session = day.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
        session.GetProperty("note").GetString().ShouldBe("مخصوص مبتدی‌ها");
        session.GetProperty("dateNote").GetString().ShouldBe("حوله بیاورید");
    }

    // ---- The tables hold it too ----

    [Fact]
    public async Task Insert_SecondNoteOnTheSameSession_RejectedByTheUniqueIndex()
    {
        var gym = await GymAsync();
        using var written = await SetNoteAsync(gym, gym.Saturday, 13, "یادداشت");

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertNoteAsync(gym, gym.Saturday, "دوباره"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe("ix_session_notes_trainer_id_date_start_time_end_time");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" یادداشت")]
    public async Task Insert_ABlankOrUntrimmedNote_RejectedByTheCheckConstraint(string text)
    {
        var gym = await GymAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertNoteAsync(gym, gym.Saturday, text));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_session_notes_text_not_blank");
    }

    [Fact]
    public async Task Update_ABlankStandingNote_RejectedByTheCheckConstraint()
    {
        await GymAsync(saturdayNote: "مخصوص مبتدی‌ها");
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = () => db.Database.ExecuteSqlAsync($"UPDATE week_pattern_sessions SET note = '  ' WHERE note IS NOT NULL", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<PostgresException>(update)).ConstraintName.ShouldBe("ck_week_pattern_sessions_note_not_blank");
    }

    // ---- Helpers ----

    /// <summary>
    /// A trainer and their student, both signed in, and the trainer's week from tomorrow: Saturday
    /// 13:00 (with <paramref name="saturdayNote"/>) and 17:00, Monday 13:00.
    /// </summary>
    private async Task<Gym> GymAsync(string? saturdayNote = null)
    {
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var tomorrow = Today().AddDays(1);
        var gym = new Gym(client, trainerToken, memberToken, trainer.Id, member.Id, tomorrow, Next(DayOfWeek.Saturday, tomorrow));

        await LinkAsync(gym);

        using var pattern = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", PatternBody(tomorrow, saturdayNote));
        pattern.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    private static object PatternBody(DateOnly startsOn, string? saturdayNote) => new
    {
        startsOn = Iso(startsOn),
        sessions = new[] { Weekly(DayOfWeek.Saturday, 13, 2, saturdayNote), Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2) },
    };

    /// <summary>The student in the Saturday 13:00 session for two weeks, so two Saturdays.</summary>
    private static async Task ScheduleSaturdaysAsync(Gym gym)
    {
        using var set = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/coaching/students/{gym.MemberId}/schedule", new
        {
            startsOn = Iso(gym.Tomorrow),
            endsOn = Iso(gym.Tomorrow.AddDays(13)),
            slots = new[] { new { day = "Saturday", start = "13:00:00", end = "14:00:00" } },
        });
        set.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Yesterday changed by hand to one 13:00 session with a standing note, and the date's note
    /// <paramref name="note"/> on it: written straight to the database, as the API refuses a day that has passed.
    /// </summary>
    /// <returns>Yesterday.</returns>
    private async Task<DateOnly> PastDayAsync(Gym gym, string? note)
    {
        var yesterday = Today().AddDays(-1);
        var before = yesterday.AddDays(-1);
        var noon = new TimeOnly(12, 0);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = new PlannedSession(new TimeOnly(13, 0), new TimeOnly(14, 0), 5, Note: "مخصوص مبتدی‌ها");
        db.TimetableDays.Add(TimetableDay.Create(gym.TrainerId, yesterday, [], [session], before, noon).Value);
        if (note is not null)
        {
            db.SessionNotes.Add(SessionNote.Write(gym.TrainerId, yesterday, session.Start, session.End, note, before, noon).Value);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return yesterday;
    }

    private async Task InsertNoteAsync(Gym gym, DateOnly date, string text)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO session_notes (id, trainer_id, date, start_time, end_time, text, created_at)
            VALUES ({Guid.CreateVersion7()}, {gym.TrainerId}, {date}, {new TimeOnly(13, 0)}, {new TimeOnly(14, 0)}, {text}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    /// <summary>The member asks for leave from that date and the trainer accepts (15.7i).</summary>
    private static async Task LeaveAcceptedAsync(Gym gym, DateOnly date, int hour)
    {
        using var asked = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/leave", new { date = Iso(date), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00" }, gym.MemberToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        var requestId = (await GetJsonAsync(gym, "/api/site/coaching/leave-requests")).GetProperty("requests").EnumerateArray()
            .Single(request => request.GetProperty("date").GetString() == Iso(date))
            .GetProperty("requestId").GetGuid();

        using var accepted = await gym.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/site/coaching/leave-requests/{requestId}/accept").WithBearer(gym.TrainerToken),
            TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The member asks the trainer, and the trainer accepts.</summary>
    private static async Task LinkAsync(Gym gym)
    {
        using var asked = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/coaching/my-trainer/request", new { trainerId = gym.TrainerId }, gym.MemberToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestId = (await asked.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();

        using var accepted = await gym.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/site/coaching/requests/{requestId}/accept").WithBearer(gym.TrainerToken),
            TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> SetNoteAsync(Gym gym, DateOnly date, int hour, string text) =>
        SendJsonAsync(gym, HttpMethod.Put, NotePath(date), NoteBody(hour, text));

    private static string NotePath(DateOnly date) => $"/api/site/timetable/days/{Iso(date)}/note";

    private static string StudentsPath(DateOnly date) => $"/api/site/timetable/days/{Iso(date)}/students";

    private static object NoteBody(int hour, string text) => new { start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", text };

    private static object Times(int hour) => new { start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00" };

    private static object Weekly(DayOfWeek day, int hour, int capacity, string? note = null) =>
        new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", capacity, note };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The first <paramref name="day"/> on or after <paramref name="from"/>.</summary>
    private static DateOnly Next(DayOfWeek day, DateOnly from) => from.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);

    private static JsonElement Day(JsonElement timetable, DateOnly date) =>
        timetable.GetProperty("days").EnumerateArray().Single(day => day.GetProperty("date").GetString() == Iso(date));

    private static JsonElement DaySession(JsonElement timetable, DateOnly date, string start) =>
        Day(timetable, date).GetProperty("sessions").EnumerateArray().Single(session => session.GetProperty("start").GetString() == start);

    private static JsonElement RosterSession(JsonElement roster, string start) =>
        roster.GetProperty("sessions").EnumerateArray().Single(session => session.GetProperty("start").GetString() == start);

    private static JsonElement Place(JsonElement places, DateOnly date) =>
        places.EnumerateArray().Single(place => place.GetProperty("date").GetString() == Iso(date));

    /// <summary>Sends <paramref name="body"/> as the trainer, or as <paramref name="token"/>'s account.</summary>
    private static Task<HttpResponseMessage> SendJsonAsync(Gym gym, HttpMethod method, string path, object body, string? token = null)
    {
        var request = new HttpRequestMessage(method, path).WithBearer(token ?? gym.TrainerToken);
        request.Content = JsonContent.Create(body);

        return gym.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads <paramref name="path"/> as the trainer, or as <paramref name="token"/>'s account.</summary>
    private static async Task<JsonElement> GetJsonAsync(Gym gym, string path, string? token = null)
    {
        using var response = await gym.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token ?? gym.TrainerToken), TestContext.Current.CancellationToken);
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

    private async Task<Trainer> AddTrainerAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = Trainer.Create("رضا محمدی", TrainerPhone, new DateOnly(2026, 10, 1)).Value;
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return trainer;
    }

    private async Task<List<SessionNote>> NotesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionNotes.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string?>> PatternNotesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().WeekPatterns.AsNoTracking()
            .SelectMany(pattern => pattern.Sessions)
            .Select(session => session.Note)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CancellationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionCancellations.CountAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Gym(
        HttpClient Client,
        string TrainerToken,
        string MemberToken,
        Guid TrainerId,
        Guid MemberId,
        DateOnly Tomorrow,
        DateOnly Saturday);
}

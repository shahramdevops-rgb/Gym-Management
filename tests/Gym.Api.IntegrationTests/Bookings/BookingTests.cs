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

namespace Gym.Api.IntegrationTests.Bookings;

/// <summary>
/// Students in the trainer's sessions on the site (BUSINESS_RULES.md §15 <i>Students in sessions</i>,
/// task 15.6b): the trainer's fixed schedule for a student, the member leaving or moving one date,
/// the capacity as a ceiling, and the warning before a change of the timetable.
/// </summary>
/// <remarks>
/// The clock here is the real one, so the sessions are tomorrow or later, where the time of day
/// plays no part; a session that has started is the Domain tests'.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class BookingTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string MemberPhone = "+989121234567";
    private const string OtherMemberPhone = "+989121234568";

    private const string MySessionsPath = "/api/site/me/sessions";

    // ---- The trainer's fixed schedule ----

    [Fact]
    public async Task SetSchedule_AStudentNow_PutsThemInEveryDateOfIt()
    {
        var gym = await GymAsync();

        using var response = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(27), Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("schedule").GetProperty("startsOn").GetString().ShouldBe(Iso(gym.Tomorrow));
        body.GetProperty("schedule").GetProperty("slots").GetArrayLength().ShouldBe(2);
        Dates(body.GetProperty("sessions")).ShouldBe(
            [gym.Saturday, gym.Monday, gym.Saturday.AddDays(7), gym.Monday.AddDays(7), gym.Saturday.AddDays(14), gym.Monday.AddDays(14), gym.Saturday.AddDays(21), gym.Monday.AddDays(21)],
            ignoreOrder: true);

        var timetable = await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken);
        Students(timetable, gym.Saturday, "13:00:00").ShouldBe(1);
        Students(timetable, gym.Saturday, "17:00:00").ShouldBe(0);
    }

    [Fact]
    public async Task SetSchedule_AMemberWhoIsNotTheirStudent_Returns422()
    {
        var gym = await GymAsync();
        var stranger = await AddMemberAsync(OtherMemberPhone);

        using var response = await SetScheduleAsync(gym, stranger.Id, gym.Tomorrow, gym.Tomorrow.AddDays(7), Slot(DayOfWeek.Saturday, 13));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotAStudent.Code);
    }

    [Fact]
    public async Task SetSchedule_ASessionNotInTheTimetable_Returns422()
    {
        var gym = await GymAsync();

        using var response = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Tuesday, 13));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NoSuchSession.Code);
        (await SchedulesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetSchedule_ADateWithNoRoom_Returns422AndSavesNothing()
    {
        var gym = await GymAsync(saturdayCapacity: 1);
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var first = await SetScheduleAsync(gym, other, gym.Saturday.AddDays(7), gym.Saturday.AddDays(7), Slot(DayOfWeek.Saturday, 13));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var check = await SendJsonAsync(gym, HttpMethod.Post, $"{SchedulePath(gym.MemberId)}/check",
            ScheduleBody(gym.Tomorrow, gym.Tomorrow.AddDays(20), Slot(DayOfWeek.Saturday, 13)));
        using var save = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(20), Slot(DayOfWeek.Saturday, 13));

        check.StatusCode.ShouldBe(HttpStatusCode.OK);
        var checkBody = await check.ReadJsonAsync();
        checkBody.GetProperty("sessions").GetInt32().ShouldBe(3);
        checkBody.GetProperty("full").EnumerateArray().Select(full => full.GetProperty("date").GetString()).ShouldBe([Iso(gym.Saturday.AddDays(7))]);

        save.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await save.ReadErrorCodeAsync()).ShouldBe(BookingErrors.SessionFull.Code);
        (await SchedulesAsync()).ShouldHaveSingleItem().MemberId.ShouldBe(other);
    }

    [Fact]
    public async Task SetSchedule_DatesWithoutTheSession_ArePassedOverAndNeverReadAsRemoved()
    {
        var gym = await GymAsync();
        var today = gym.Tomorrow.AddDays(-1);
        using var closed = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(gym.Saturday)}", new { sessions = Array.Empty<object>() });
        closed.StatusCode.ShouldBe(HttpStatusCode.OK);

        // From today, before the week starts: today has no session either.
        using var set = await SetScheduleAsync(gym, gym.MemberId, today, gym.Saturday.AddDays(7), Slot(DayOfWeek.Saturday, 13));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessions = (await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken)).GetProperty("sessions");
        Dates(sessions).ShouldBe([gym.Saturday.AddDays(7)]);
        sessions.EnumerateArray().ShouldAllBe(session => !session.GetProperty("removed").GetBoolean());
        (await SkipsAsync()).Select(skip => skip.Date).ShouldContain(gym.Saturday);
        (await SkipsAsync()).ShouldAllBe(skip => skip.By == BookedBy.Trainer);
    }

    [Fact]
    public async Task CheckSchedule_SavesNothing()
    {
        var gym = await GymAsync();

        using var check = await SendJsonAsync(gym, HttpMethod.Post, $"{SchedulePath(gym.MemberId)}/check",
            ScheduleBody(gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13)));

        check.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await check.ReadJsonAsync();
        body.GetProperty("sessions").GetInt32().ShouldBe(2);
        body.GetProperty("full").GetArrayLength().ShouldBe(0);
        (await SchedulesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetSchedule_NotStartedYet_IsReplacedWhole()
    {
        var gym = await GymAsync();
        using var first = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(27), Slot(DayOfWeek.Saturday, 13));

        using var second = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Monday, 13));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schedule = (await SchedulesAsync()).ShouldHaveSingleItem();
        schedule.Slots.ShouldHaveSingleItem().Day.ShouldBe(DayOfWeek.Monday);
        schedule.EndsOn.ShouldBe(gym.Tomorrow.AddDays(13));
    }

    [Fact]
    public async Task SetSchedule_OneFromToday_IsReplacedFromNow()
    {
        var gym = await GymAsync();
        var today = gym.Tomorrow.AddDays(-1);
        using var first = await SetScheduleAsync(gym, gym.MemberId, today, today.AddDays(27), Slot(DayOfWeek.Saturday, 13));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var again = await SetScheduleAsync(gym, gym.MemberId, today, today.AddDays(27), Slot(DayOfWeek.Monday, 13));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schedule = (await SchedulesAsync()).ShouldHaveSingleItem();
        (schedule.StartsOn, schedule.EndsOn).ShouldBe((today, today.AddDays(27)));
        schedule.Slots.ShouldHaveSingleItem().Day.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public async Task SetSchedule_OneWithEarlierDates_EndsYesterdayAndKeepsTodaysStartedSession()
    {
        var gym = await GymAsync();
        var today = await StartedTodayAsync(gym);

        using var changed = await SetScheduleAsync(gym, gym.MemberId, today, today.AddDays(27), Slot(DayOfWeek.Monday, 13));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schedules = (await SchedulesAsync()).OrderBy(schedule => schedule.StartsOn).ToList();
        schedules.Select(schedule => (schedule.StartsOn, schedule.EndsOn)).ShouldBe([(today.AddDays(-7), today.AddDays(-1)), (today, today.AddDays(27))]);
        KeptMidnight(await BookingsAsync(), today);
        var midnight = (await changed.ReadJsonAsync()).GetProperty("sessions").EnumerateArray()
            .Single(session => session.GetProperty("date").GetString() == Iso(today) && session.GetProperty("start").GetString() == "00:00:00");
        midnight.GetProperty("moved").GetBoolean().ShouldBeFalse();
        midnight.GetProperty("started").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task EndSchedule_FromNow_KeepsTodaysStartedSession()
    {
        var gym = await GymAsync();
        var today = await StartedTodayAsync(gym);

        using var ended = await gym.Client.SendAsync(Send(HttpMethod.Delete, SchedulePath(gym.MemberId), gym.TrainerToken), TestContext.Current.CancellationToken);

        ended.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SchedulesAsync()).ShouldHaveSingleItem().EndsOn.ShouldBe(today.AddDays(-1));
        KeptMidnight(await BookingsAsync(), today);
    }

    [Fact]
    public async Task EndSchedule_TheDatesAfterTodayAreGone_AndASecondEndReturns404()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(27), Slot(DayOfWeek.Saturday, 13));

        using var ended = await gym.Client.SendAsync(Send(HttpMethod.Delete, SchedulePath(gym.MemberId), gym.TrainerToken), TestContext.Current.CancellationToken);
        using var again = await gym.Client.SendAsync(Send(HttpMethod.Delete, SchedulePath(gym.MemberId), gym.TrainerToken), TestContext.Current.CancellationToken);

        ended.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ended.ReadJsonAsync();
        body.GetProperty("schedule").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("sessions").GetArrayLength().ShouldBe(0);
        (await SchedulesAsync()).ShouldBeEmpty();
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await again.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NoSchedule.Code);
    }

    [Fact]
    public async Task Schedule_AMemberToken_Returns403()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, SchedulePath(gym.MemberId), gym.MemberToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The member's sessions ----

    [Fact]
    public async Task MySessions_TheirDatesAndWhereTheyCanMoveOne()
    {
        var gym = await GymAsync(saturdayCapacity: 1);
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var theirs = await SetScheduleAsync(gym, other, gym.Saturday, gym.Saturday, Slot(DayOfWeek.Saturday, 13));
        var lastDate = gym.Monday.AddDays(6);
        using var mine = await SetScheduleAsync(gym, gym.MemberId, gym.Monday, lastDate, Slot(DayOfWeek.Monday, 13));

        var body = await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken);

        body.GetProperty("trainerName").GetString().ShouldBe("رضا محمدی");
        body.GetProperty("schedule").GetProperty("endsOn").GetString().ShouldBe(Iso(lastDate));
        Dates(body.GetProperty("sessions")).ShouldBe([gym.Monday]);
        var openings = body.GetProperty("openings").EnumerateArray()
            .Select(opening => (Date: DateOnly.Parse(opening.GetProperty("date").GetString()!, CultureInfo.InvariantCulture), Start: opening.GetProperty("start").GetString()))
            .ToList();
        openings.ShouldNotContain((gym.Saturday, "13:00:00"));
        openings.ShouldNotContain((gym.Monday, "13:00:00"));
        openings.ShouldContain((gym.Saturday, "17:00:00"));
        openings.ShouldAllBe(opening => opening.Date <= lastDate);
    }

    [Fact]
    public async Task MySessions_AVipSession_IsMarkedForTheStudentAndTheTrainer()
    {
        var gym = await GymAsync();
        using var vip = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(gym.Monday)}", new
        {
            sessions = new[]
            {
                new { start = "13:00:00", end = "14:00:00", capacity = 2, isVip = true },
                new { start = "18:00:00", end = "19:00:00", capacity = 2, isVip = true },
            },
        });
        vip.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var mine = await SetScheduleAsync(gym, gym.MemberId, gym.Monday, gym.Monday.AddDays(6), Slot(DayOfWeek.Monday, 13));

        var body = await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken);

        body.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem().GetProperty("isVip").GetBoolean().ShouldBeTrue();
        var openings = body.GetProperty("openings").EnumerateArray()
            .Select(opening => (Date: opening.GetProperty("date").GetString(), Start: opening.GetProperty("start").GetString(), IsVip: opening.GetProperty("isVip").GetBoolean()))
            .ToList();
        openings.ShouldContain((Iso(gym.Monday), "18:00:00", true));
        openings.ShouldContain((Iso(gym.Saturday), "17:00:00", false));
        var students = await GetJsonAsync(gym.Client, StudentsPath(gym.Monday), gym.TrainerToken);
        students.GetProperty("sessions")[0].GetProperty("isVip").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task MySessions_WithoutATrainer_IsEmpty()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        var body = await GetJsonAsync(client, MySessionsPath, token);

        body.GetProperty("trainerName").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("sessions").GetArrayLength().ShouldBe(0);
        body.GetProperty("openings").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Leave_OneDate_IsGoneAndTheOtherWeeksStay()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));

        using var left = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 13), gym.MemberToken);
        using var again = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 13), gym.MemberToken);

        left.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessions = (await left.ReadJsonAsync()).GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(session => (session.GetProperty("date").GetString(), session.GetProperty("left").GetBoolean()))
            .ShouldBe([(Iso(gym.Saturday), true), (Iso(gym.Saturday.AddDays(7)), false)]);
        var skip = (await SkipsAsync()).ShouldHaveSingleItem();
        (skip.By, skip.Reason).ShouldBe((BookedBy.Member, SkipReason.Left));
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await again.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotInSession.Code);
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "13:00:00").ShouldBe(0);
    }

    [Fact]
    public async Task Return_ADateTheyLeft_ComesBack()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        using var left = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 13), gym.MemberToken);

        using var back = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/return", Place(gym.Saturday, 13), gym.MemberToken);

        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessions = (await back.ReadJsonAsync()).GetProperty("sessions");
        sessions.EnumerateArray().ShouldAllBe(session => !session.GetProperty("left").GetBoolean());
        Dates(sessions).ShouldBe([gym.Saturday, gym.Saturday.AddDays(7)]);
        (await SkipsAsync()).ShouldBeEmpty();
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "13:00:00").ShouldBe(1);
    }

    [Fact]
    public async Task Return_ADateMovedOrTakenOut_Returns404NotLeft()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);
        using var removed = await SendJsonAsync(
            gym, HttpMethod.Post, $"{StudentsPath(gym.Saturday.AddDays(7))}/remove", new { memberId = gym.MemberId, start = "13:00:00", end = "14:00:00" });
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var fromMove = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/return", Place(gym.Saturday, 13), gym.MemberToken);
        using var fromTrainer = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/return", Place(gym.Saturday.AddDays(7), 13), gym.MemberToken);

        fromMove.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await fromMove.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotLeft.Code);
        fromTrainer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await fromTrainer.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotLeft.Code);
        (await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken)).GetProperty("sessions").EnumerateArray()
            .ShouldAllBe(session => !session.GetProperty("left").GetBoolean());
    }

    [Fact]
    public async Task Return_ASessionFilledMeanwhile_Returns422AndStaysLeft()
    {
        var gym = await GymAsync(saturdayCapacity: 1);
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Saturday, gym.Saturday, Slot(DayOfWeek.Saturday, 13));
        using var left = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 13), gym.MemberToken);
        using var theirs = await SetScheduleAsync(gym, other, gym.Saturday, gym.Saturday, Slot(DayOfWeek.Saturday, 13));
        theirs.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var back = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/return", Place(gym.Saturday, 13), gym.MemberToken);

        back.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await back.ReadErrorCodeAsync()).ShouldBe(BookingErrors.SessionFull.Code);
        (await SkipsAsync()).ShouldHaveSingleItem().Reason.ShouldBe(SkipReason.Left);
    }

    [Fact]
    public async Task Move_ToASessionWithRoom_LeavesTheFirstAndTakesTheSecond()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));

        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);

        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessions = (await moved.ReadJsonAsync()).GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(session => (session.GetProperty("date").GetString(), session.GetProperty("start").GetString(), session.GetProperty("moved").GetBoolean()))
            .ShouldBe([(Iso(gym.Saturday), "17:00:00", true), (Iso(gym.Saturday.AddDays(7)), "13:00:00", false)]);
        (await BookingsAsync()).ShouldHaveSingleItem().By.ShouldBe(BookedBy.Member);
        (await SkipsAsync()).ShouldHaveSingleItem().Date.ShouldBe(gym.Saturday);
    }

    [Fact]
    public async Task Move_ADateMovedTo_MovesAgainWithoutASkip()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        using var first = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);

        using var second = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 17, gym.Monday, 13), gym.MemberToken);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BookingsAsync()).ShouldHaveSingleItem().Date.ShouldBe(gym.Monday);
        (await SkipsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Move_ToAFullSession_Returns422AndChangesNothing()
    {
        var gym = await GymAsync(saturdayCapacity: 1);
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var theirs = await SetScheduleAsync(gym, other, gym.Saturday.AddDays(7), gym.Saturday.AddDays(7), Slot(DayOfWeek.Saturday, 13));
        using var mine = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Monday, 13));

        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Monday, 13, gym.Saturday.AddDays(7), 13), gym.MemberToken);

        moved.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await moved.ReadErrorCodeAsync()).ShouldBe(BookingErrors.SessionFull.Code);
        (await SkipsAsync()).ShouldBeEmpty();
        (await BookingsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Move_AfterTheScheduleEnds_Returns422()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Saturday, Slot(DayOfWeek.Saturday, 13));

        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday.AddDays(7), 13), gym.MemberToken);

        moved.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await moved.ReadErrorCodeAsync()).ShouldBe(BookingErrors.MoveOutOfSchedule.Code);
    }

    [Fact]
    public async Task EndMyTrainer_TheSessionsAfterTodayAreGone()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));

        using var ended = await gym.Client.SendAsync(Send(HttpMethod.Post, "/api/site/coaching/my-trainer/end", gym.MemberToken), TestContext.Current.CancellationToken);

        ended.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken)).GetProperty("sessions").GetArrayLength().ShouldBe(0);
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "13:00:00").ShouldBe(0);
    }

    // ---- «شاگردان سانس» ----

    [Fact]
    public async Task SessionStudents_EachSessionWithItsStudents()
    {
        var gym = await GymAsync();
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var mine = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        using var theirs = await SetScheduleAsync(gym, other, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 17));

        var body = await GetJsonAsync(gym.Client, StudentsPath(gym.Saturday), gym.TrainerToken);

        var sessions = body.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(session => session.GetProperty("start").GetString()).ShouldBe(["13:00:00", "17:00:00"]);
        sessions[0].GetProperty("capacity").GetInt32().ShouldBe(2);
        var student = sessions[0].GetProperty("students").EnumerateArray().ShouldHaveSingleItem();
        student.GetProperty("memberId").GetGuid().ShouldBe(gym.MemberId);
        student.GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);
        sessions[1].GetProperty("students")[0].GetProperty("memberId").GetGuid().ShouldBe(other);
    }

    [Fact]
    public async Task RemoveSessionStudent_TheTrainerTakesThemOutOfOneDate()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));

        using var removed = await SendJsonAsync(gym, HttpMethod.Post, $"{StudentsPath(gym.Saturday)}/remove",
            new { memberId = gym.MemberId, start = "13:00:00", end = "14:00:00" });

        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.ReadJsonAsync()).GetProperty("sessions")[0].GetProperty("students").GetArrayLength().ShouldBe(0);
        (await SkipsAsync()).ShouldHaveSingleItem().By.ShouldBe(BookedBy.Trainer);
        Dates((await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken)).GetProperty("sessions")).ShouldBe([gym.Saturday.AddDays(7)]);
    }

    [Fact]
    public async Task SessionStudents_ADayPastTheTimetable_Returns422()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(
            Send(HttpMethod.Get, StudentsPath(gym.Tomorrow.AddDays(TimetableRules.DaysAhead)), gym.TrainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(TimetableErrors.DayOutOfRange.Code);
    }

    // ---- The warning before a change of the timetable ----

    [Fact]
    public async Task CheckPattern_RemovingASessionWithStudents_CountsThem_AndAfterTheSaveTheyAreMarkedRemoved()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));
        var pattern = new
        {
            startsOn = Iso(gym.Tomorrow),
            sessions = new[] { Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2) },
        };

        using var check = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/timetable/pattern/check", pattern);
        using var save = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", pattern);

        check.StatusCode.ShouldBe(HttpStatusCode.OK);
        var impact = await check.ReadJsonAsync();
        impact.GetProperty("students").GetInt32().ShouldBe(1);
        impact.GetProperty("sessions").EnumerateArray().Select(session => session.GetProperty("removed").GetBoolean()).ShouldBe([true, true]);
        impact.GetProperty("toReschedule").EnumerateArray().Select(student => student.GetProperty("memberId").GetGuid()).ShouldBe([gym.MemberId]);
        save.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessions = (await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken)).GetProperty("sessions").EnumerateArray().ToList();
        sessions.Count.ShouldBe(2);
        sessions.ShouldAllBe(session => session.GetProperty("removed").GetBoolean());
    }

    [Fact]
    public async Task CheckDay_ACapacityBelowItsStudents_IsTouchedNotRemoved()
    {
        var gym = await GymAsync();
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        using var mine = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(6), Slot(DayOfWeek.Saturday, 13));
        using var theirs = await SetScheduleAsync(gym, other, gym.Tomorrow, gym.Tomorrow.AddDays(6), Slot(DayOfWeek.Saturday, 13));

        using var check = await SendJsonAsync(gym, HttpMethod.Post, $"/api/site/timetable/days/{Iso(gym.Saturday)}/check",
            new { sessions = new[] { new { start = "13:00:00", end = "14:00:00", capacity = 1 } } });

        check.StatusCode.ShouldBe(HttpStatusCode.OK);
        var impact = await check.ReadJsonAsync();
        impact.GetProperty("students").GetInt32().ShouldBe(2);
        var session = impact.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
        session.GetProperty("removed").GetBoolean().ShouldBeFalse();
        session.GetProperty("students").GetInt32().ShouldBe(2);
        impact.GetProperty("toReschedule").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task CheckReset_ADateNeverChanged_TouchesNoOne()
    {
        var gym = await GymAsync();

        var impact = await GetJsonAsync(gym.Client, $"/api/site/timetable/days/{Iso(gym.Saturday)}/reset-check", gym.TrainerToken);

        impact.GetProperty("students").GetInt32().ShouldBe(0);
    }

    // ---- The database ----

    [Fact]
    public async Task Database_TwoSchedulesOfOneLinkOnTheSameDates_AreRefused()
    {
        var gym = await GymAsync();
        using var set = await SetScheduleAsync(gym, gym.MemberId, gym.Tomorrow, gym.Tomorrow.AddDays(13), Slot(DayOfWeek.Saturday, 13));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await db.TrainerLinks.SingleAsync(row => row.MemberId == gym.MemberId, TestContext.Current.CancellationToken);
        db.FixedSchedules.Add(FixedSchedule.Create(
            gym.TrainerId, gym.MemberId, link.Id, gym.Tomorrow.AddDays(7), gym.Tomorrow.AddDays(20), [new WeeklySlot(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(14, 0))], gym.Tomorrow.AddDays(-1)).Value);

        var save = () => db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ExclusionConstraintException>(save)).ConstraintName.ShouldBe("ex_fixed_schedules_link_id_dates");
    }

    // ---- Helpers ----

    /// <summary>
    /// A trainer and their student, both signed in, and the trainer's week from tomorrow: Saturday
    /// 13:00 and 17:00, Monday 13:00.
    /// </summary>
    private async Task<Gym> GymAsync(int saturdayCapacity = 2)
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync();
        var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var tomorrow = Today().AddDays(1);
        var gym = new Gym(client, trainerToken, memberToken, trainer.Id, member.Id, tomorrow, Next(DayOfWeek.Saturday, tomorrow), Next(DayOfWeek.Monday, tomorrow));

        await LinkAsync(gym, memberToken);

        using var pattern = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", new
        {
            startsOn = Iso(tomorrow),
            sessions = new[] { Weekly(DayOfWeek.Saturday, 13, saturdayCapacity), Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2) },
        });
        pattern.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    /// <summary>
    /// Today with a session from 00:00 to 01:00, which has started (the clock here is the real one),
    /// and the student in it by a fixed schedule from a week ago. Written straight to the database:
    /// neither can be made through the API once the session has started.
    /// </summary>
    /// <returns>Today.</returns>
    private async Task<DateOnly> StartedTodayAsync(Gym gym)
    {
        var today = Today();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkId = await db.TrainerLinks.Where(link => link.MemberId == gym.MemberId).Select(link => link.Id).SingleAsync(TestContext.Current.CancellationToken);

        var midnight = new PlannedSession(TimeOnly.MinValue, new TimeOnly(1, 0), 5);
        db.TimetableDays.Add(TimetableDay.Create(gym.TrainerId, today, [], [midnight], today.AddDays(-1), new TimeOnly(12, 0)).Value);
        db.FixedSchedules.Add(FixedSchedule.Create(
            gym.TrainerId, gym.MemberId, linkId, today.AddDays(-7), today.AddDays(13),
            [new WeeklySlot(today.DayOfWeek, midnight.Start, midnight.End)], today.AddDays(-7)).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return today;
    }

    /// <summary>Today's started session, kept for the student as the trainer's single booking.</summary>
    private static void KeptMidnight(List<SessionBooking> bookings, DateOnly today)
    {
        var kept = bookings.ShouldHaveSingleItem();
        (kept.Date, kept.Start, kept.By).ShouldBe((today, TimeOnly.MinValue, BookedBy.Trainer));
    }

    /// <summary>Another member made the trainer's student, as <see cref="GymAsync"/>'s is.</summary>
    private async Task<Guid> AddStudentAsync(Gym gym, string phone)
    {
        var member = await AddMemberAsync(phone);
        await LinkAsync(gym, await SignInAsync(gym.Client, phone));

        return member.Id;
    }

    /// <summary>The member asks the trainer, and the trainer accepts.</summary>
    private static async Task LinkAsync(Gym gym, string memberToken)
    {
        var ask = Send(HttpMethod.Post, "/api/site/coaching/my-trainer/request", memberToken);
        ask.Content = JsonContent.Create(new { trainerId = gym.TrainerId });
        using var asked = await gym.Client.SendAsync(ask, TestContext.Current.CancellationToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestId = (await asked.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();

        using var accepted = await gym.Client.SendAsync(
            Send(HttpMethod.Post, $"/api/site/coaching/requests/{requestId}/accept", gym.TrainerToken), TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string SchedulePath(Guid memberId) => $"/api/site/coaching/students/{memberId}/schedule";

    private static string StudentsPath(DateOnly date) => $"/api/site/timetable/days/{Iso(date)}/students";

    private static object Weekly(DayOfWeek day, int hour, int capacity) =>
        new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", capacity };

    private static object Slot(DayOfWeek day, int hour) => new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00" };

    private static object ScheduleBody(DateOnly startsOn, DateOnly endsOn, params object[] slots) =>
        new { startsOn = Iso(startsOn), endsOn = Iso(endsOn), slots };

    private static object Place(DateOnly date, int hour) => new { date = Iso(date), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00" };

    private static object Move(DateOnly date, int hour, DateOnly toDate, int toHour) => new
    {
        date = Iso(date),
        start = $"{hour:00}:00:00",
        end = $"{hour + 1:00}:00:00",
        toDate = Iso(toDate),
        toStart = $"{toHour:00}:00:00",
        toEnd = $"{toHour + 1:00}:00:00",
    };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The first <paramref name="day"/> on or after <paramref name="from"/>.</summary>
    private static DateOnly Next(DayOfWeek day, DateOnly from) => from.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);

    private static List<DateOnly> Dates(JsonElement sessions) =>
        sessions.EnumerateArray().Select(session => DateOnly.Parse(session.GetProperty("date").GetString()!, CultureInfo.InvariantCulture)).ToList();

    /// <summary>The students of the session of <paramref name="date"/> starting at <paramref name="start"/>, in «برنامه سانس‌ها».</summary>
    private static int Students(JsonElement timetable, DateOnly date, string start) =>
        timetable.GetProperty("days").EnumerateArray()
            .Single(day => day.GetProperty("date").GetString() == Iso(date))
            .GetProperty("sessions").EnumerateArray()
            .Single(session => session.GetProperty("start").GetString() == start)
            .GetProperty("students").GetInt32();

    private static HttpRequestMessage Send(HttpMethod method, string path, string token) => new HttpRequestMessage(method, path).WithBearer(token);

    private static Task<HttpResponseMessage> SetScheduleAsync(Gym gym, Guid memberId, DateOnly startsOn, DateOnly endsOn, params object[] slots) =>
        SendJsonAsync(gym, HttpMethod.Put, SchedulePath(memberId), ScheduleBody(startsOn, endsOn, slots));

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

    private async Task<Member> AddMemberAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("علی کریمی", phone);
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

    private async Task<List<FixedSchedule>> SchedulesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().FixedSchedules.AsNoTracking()
            .Include(schedule => schedule.Slots)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SessionSkip>> SkipsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionSkips.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SessionBooking>> BookingsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionBookings.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Gym(
        HttpClient Client,
        string TrainerToken,
        string MemberToken,
        Guid TrainerId,
        Guid MemberId,
        DateOnly Tomorrow,
        DateOnly Saturday,
        DateOnly Monday);
}

using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Bookings;
using Gym.Application.Bookings.GetStudentAttendance;
using Gym.Application.Bookings.GetTrainerDay;
using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Bookings;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Bookings;

/// <summary>
/// Attendance for the trainer from the desk's check-ins (BUSINESS_RULES.md §15 <i>Attendance for
/// the trainer</i>, tasks 15.7 and 15.7d): a day (today or earlier) and a student's attendance over a range of dates.
/// </summary>
/// <remarks>
/// The API's clock is the real one, and sessions and schedules are made from tomorrow on. So the
/// timetable is built through the API as the trainer would, the check-ins are written as the desk
/// would, and the handlers are then asked on a clock set to a later day (as SessionCancellationTests
/// does for its job). The endpoints themselves are asked only for what does not depend on the hour.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PresenceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string MemberPhone = "+989121234567";
    private const string OtherMemberPhone = "+989121234568";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private readonly Dictionary<Guid, Guid> _plans = [];

    private int _lockerNumber;

    // ---- «امروز» ----

    [Fact]
    public async Task Today_CheckInWithinTheQuarter_IsOnTime()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 15));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        today.Date.ShouldBe(gym.Saturday);
        var session = today.Sessions.Single(row => row.Start == new TimeOnly(13, 0));
        (session.Removed, session.Started, session.Ended).ShouldBe((false, true, true));
        var student = session.Students.ShouldHaveSingleItem();
        (student.MemberId, student.State, student.CheckedInAt).ShouldBe((gym.MemberId, SessionPresence.OnTime, new TimeOnly(13, 15)));
        today.Sessions.Single(row => row.Start == new TimeOnly(17, 0)).ShouldSatisfyAllConditions(
            later => later.Students.ShouldBeEmpty(),
            later => later.Started.ShouldBeFalse());
    }

    [Fact]
    public async Task Today_CheckInAfterTheQuarter_IsLate()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 16));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        var student = StudentAt(today, 13);
        (student.State, student.MinutesLate).ShouldBe((SessionPresence.Late, 16));
        today.Totals.ShouldBe(new DayTotals(Students: 1, Present: 1, Late: 1, CameAfter: 0, Absent: 0));
    }

    [Fact]
    public async Task Today_NoCheckInWhileTheSessionRuns_IsNotYet()
    {
        var gym = await GymAsync();

        var today = await TodayAsync(gym, At(gym.Saturday, 13, 30));

        StudentAt(today, 13).State.ShouldBe(SessionPresence.NotYet);
        today.Totals.ShouldBe(new DayTotals(Students: 1, Present: 0, Late: 0, CameAfter: 0, Absent: 0));
    }

    [Fact]
    public async Task Today_CheckInAfterTheEnd_IsCameAfterNotAbsent()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 16, 0));

        var today = await TodayAsync(gym, At(gym.Saturday, 18, 0));

        var student = StudentAt(today, 13);
        (student.State, student.CheckedInAt).ShouldBe((SessionPresence.CameAfter, new TimeOnly(16, 0)));
        today.Totals.ShouldBe(new DayTotals(Students: 1, Present: 0, Late: 0, CameAfter: 1, Absent: 0));
    }

    [Fact]
    public async Task Today_CheckInCancelledAtTheDesk_IsAbsent()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 0), cancelled: true);

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).ShouldSatisfyAllConditions(
            student => student.State.ShouldBe(SessionPresence.Absent),
            student => student.CheckedInAt.ShouldBeNull());
    }

    [Fact]
    public async Task Today_EarlyCheckInStillInsideAtTheStart_IsOnTime()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 11, 0));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).State.ShouldBe(SessionPresence.OnTime);
    }

    [Fact]
    public async Task Today_EarlyCheckInLeftBeforeTheStart_IsAbsent()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 10, 0), checkedOutAt: At(gym.Saturday, 12, 0));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).State.ShouldBe(SessionPresence.Absent);
    }

    [Fact]
    public async Task Today_StudentLeftTheDate_IsLeaveNotAbsent()
    {
        var gym = await GymAsync();
        using var left = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/leave", Place(gym.Saturday, 13), gym.MemberToken);
        left.StatusCode.ShouldBe(HttpStatusCode.OK);

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).State.ShouldBe(SessionPresence.Leave);
        today.Totals.ShouldBe(new DayTotals(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Today_SessionRemovedByTheTrainer_IsCancelledUnderItsOldTimes()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        var removed = today.Sessions.Single(row => row.Removed);
        (removed.Start, removed.End).ShouldBe((new TimeOnly(13, 0), new TimeOnly(14, 0)));
        removed.Students.ShouldHaveSingleItem().State.ShouldBe(SessionPresence.Cancelled);
        today.Sessions.Where(row => !row.Removed).Select(row => row.Start).ShouldBe([new TimeOnly(17, 0)]);
    }

    [Fact]
    public async Task Today_MemberMovedTheDate_CountsAtTheNewSession()
    {
        var gym = await GymAsync();
        using var moved = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        await VisitAsync(gym.MemberId, At(gym.Saturday, 17, 5));

        var today = await TodayAsync(gym, At(gym.Saturday, 19, 0));

        today.Sessions.Single(row => row.Start == new TimeOnly(13, 0)).Students.ShouldBeEmpty();
        StudentAt(today, 17).State.ShouldBe(SessionPresence.OnTime);
    }

    [Fact]
    public async Task Today_OtherMembersCheckIn_DoesNotCountForTheStudent()
    {
        var gym = await GymAsync();
        var other = await AddMemberAsync(OtherMemberPhone);
        await VisitAsync(other.Id, At(gym.Saturday, 13, 0));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).State.ShouldBe(SessionPresence.Absent);
    }

    [Fact]
    public async Task GetToday_AsTheTrainer_ReturnsTodaysSessions()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, "/api/site/coaching/today", gym.TrainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("date").GetString().ShouldBe(Iso(Today()));
    }

    [Fact]
    public async Task GetToday_AsAMember_IsForbidden()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, "/api/site/coaching/today", gym.MemberToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- 15.7d: «زودتر آمد», the check-out, any earlier day, the counts, outside the sessions ----

    [Fact]
    public async Task Today_CheckInBeforeTheStart_IsOnTimeWithTheMinutesEarly()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 12, 50));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        var student = StudentAt(today, 13);
        (student.State, student.MinutesEarly, student.CheckedInAt).ShouldBe((SessionPresence.OnTime, 10, new TimeOnly(12, 50)));
        today.Totals.ShouldBe(new DayTotals(Students: 1, Present: 1, Late: 0, CameAfter: 0, Absent: 0));
    }

    [Fact]
    public async Task Today_CheckInAtTheStart_IsNotEarly()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 0));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        StudentAt(today, 13).MinutesEarly.ShouldBeNull();
    }

    [Fact]
    public async Task Today_CheckedOutAtTheDesk_ShowsTheCheckOutTime()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 5), checkedOutAt: At(gym.Saturday, 14, 40));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        var student = StudentAt(today, 13);
        (student.CheckedOutAt, student.AutoCheckedOut).ShouldBe((new TimeOnly(14, 40), false));
    }

    [Fact]
    public async Task Today_StillInside_HasNoCheckOut()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 5));

        var today = await TodayAsync(gym, At(gym.Saturday, 15, 0));

        var student = StudentAt(today, 13);
        (student.CheckedOutAt, student.AutoCheckedOut).ShouldBe((null, false));
    }

    [Fact]
    public async Task Day_VisitClosedByTheNightlyRun_ShowsNoCheckOutTime()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 5), autoClosedAt: At(gym.Saturday.AddDays(1), 0, 0));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday.AddDays(1), 10, 0));

        var student = StudentAt(day, 13);
        (student.State, student.CheckedOutAt, student.AutoCheckedOut).ShouldBe((SessionPresence.OnTime, null, true));
    }

    [Fact]
    public async Task Day_EarlierDay_ReadsThatDaysSessions()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 20), checkedOutAt: At(gym.Saturday, 14, 30));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday.AddDays(2), 9, 0));

        (day.Date, day.IsToday).ShouldBe((gym.Saturday, false));
        var session = day.Sessions.Single(row => row.Start == new TimeOnly(13, 0));
        (session.Started, session.Ended).ShouldBe((true, true));
        StudentAt(day, 13).State.ShouldBe(SessionPresence.Late);
        day.Totals.ShouldBe(new DayTotals(Students: 1, Present: 1, Late: 1, CameAfter: 0, Absent: 0));
    }

    [Fact]
    public async Task Day_Today_IsToday()
    {
        var gym = await GymAsync();

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 12, 0));

        day.IsToday.ShouldBeTrue();
    }

    [Fact]
    public async Task Day_AfterToday_IsRefused()
    {
        var gym = await GymAsync();
        await using var scope = Fixture.CreateScope();
        var handler = new GetTrainerDayHandler(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), CalendarAt(At(gym.Saturday, 12, 0)));

        var result = await handler.Handle(gym.TrainerAccountId, gym.Saturday.AddDays(1), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(BookingErrors.AttendanceDayInFuture);
    }

    [Fact]
    public async Task Day_AbsentStudent_IsCounted()
    {
        var gym = await GymAsync();

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 20, 0));

        day.Totals.ShouldBe(new DayTotals(Students: 1, Present: 0, Late: 0, CameAfter: 0, Absent: 1));
    }

    [Fact]
    public async Task Day_SessionRemoved_IsInNoCount()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 20, 0));

        day.Totals.ShouldBe(new DayTotals(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Day_CheckInOutsideTheStudentsSessions_IsListed()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 5), checkedOutAt: At(gym.Saturday, 14, 0));
        await VisitAsync(gym.MemberId, At(gym.Saturday, 17, 30), checkedOutAt: At(gym.Saturday, 18, 45));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 20, 0));

        var outside = day.Outside.ShouldHaveSingleItem();
        (outside.MemberId, outside.FullName).ShouldBe((gym.MemberId, "علی کریمی"));
        outside.Visits.ShouldBe([new VisitTimes(new TimeOnly(17, 30), new TimeOnly(18, 45), AutoCheckedOut: false)]);
    }

    [Fact]
    public async Task Day_CheckInOnADayWithNoSession_IsListed()
    {
        var gym = await GymAsync();
        var sunday = gym.Saturday.AddDays(1);
        await VisitAsync(gym.MemberId, At(sunday, 10, 0));

        var day = await DayAsync(gym, sunday, At(sunday, 20, 0));

        day.Sessions.ShouldBeEmpty();
        day.Outside.ShouldHaveSingleItem().Visits.ShouldHaveSingleItem().CheckedInAt.ShouldBe(new TimeOnly(10, 0));
    }

    [Fact]
    public async Task Day_CheckInOnALeftDate_IsListedOutside()
    {
        var gym = await GymAsync();
        using var left = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/leave", Place(gym.Saturday, 13), gym.MemberToken);
        left.StatusCode.ShouldBe(HttpStatusCode.OK);
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 0));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 20, 0));

        StudentAt(day, 13).State.ShouldBe(SessionPresence.Leave);
        day.Outside.ShouldHaveSingleItem().MemberId.ShouldBe(gym.MemberId);
    }

    [Fact]
    public async Task Day_SomeoneElsesCheckIn_IsNotListed()
    {
        var gym = await GymAsync();
        var other = await AddMemberAsync(OtherMemberPhone);
        await VisitAsync(other.Id, At(gym.Saturday, 10, 0));

        var day = await DayAsync(gym, gym.Saturday, At(gym.Saturday, 20, 0));

        day.Outside.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDay_AsTheTrainer_ReturnsThatDay()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, $"/api/site/coaching/days/{Iso(Today())}", gym.TrainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("date").GetString().ShouldBe(Iso(Today()));
        body.GetProperty("isToday").GetBoolean().ShouldBeTrue();
        body.GetProperty("totals").GetProperty("students").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetDay_Tomorrow_IsRefused()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, $"/api/site/coaching/days/{Iso(Today().AddDays(1))}", gym.TrainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.AttendanceDayInFuture.Code);
    }

    [Fact]
    public async Task GetDay_AsAMember_IsForbidden()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, $"/api/site/coaching/days/{Iso(Today())}", gym.MemberToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- A student's attendance ----

    [Fact]
    public async Task Attendance_TwoWeeks_MarksEachSessionAndCounts()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 20), checkedOutAt: At(gym.Saturday, 15, 0));
        await VisitAsync(gym.MemberId, At(gym.Saturday.AddDays(2), 12, 45));
        var nextSaturday = gym.Saturday.AddDays(7);

        var attendance = await AttendanceAsync(gym, gym.Saturday, nextSaturday.AddDays(3), At(nextSaturday, 20, 0));

        attendance.Sessions.Select(row => (row.Date, row.State)).ShouldBe(
        [
            (gym.Saturday, SessionPresence.Late),
            (gym.Saturday.AddDays(2), SessionPresence.OnTime),
            (nextSaturday, SessionPresence.Absent),
        ]);
        attendance.Sessions.ShouldAllBe(row => row.TrainerName == null);
        attendance.Totals.ShouldBe(new AttendanceTotals(OnTime: 1, Late: 1, CameAfter: 0, Absent: 1, Leave: 0, Cancelled: 0));
        attendance.Sessions[0].ShouldSatisfyAllConditions(
            first => first.CheckedOutAt.ShouldBe(new TimeOnly(15, 0)),
            first => first.MinutesLate.ShouldBe(20));
        attendance.Sessions[1].ShouldSatisfyAllConditions(
            second => second.MinutesEarly.ShouldBe(15),
            second => second.CheckedOutAt.ShouldBeNull());
        attendance.Outside.ShouldBeEmpty();
    }

    [Fact]
    public async Task Attendance_CheckInsOutsideTheSessions_AreListedByDate()
    {
        var gym = await GymAsync();
        await VisitAsync(gym.MemberId, At(gym.Saturday, 13, 5));
        await VisitAsync(gym.MemberId, At(gym.Saturday, 18, 0), checkedOutAt: At(gym.Saturday, 19, 30));
        await VisitAsync(gym.MemberId, At(gym.Saturday.AddDays(1), 10, 0), autoClosedAt: At(gym.Saturday.AddDays(2), 0, 0));

        var attendance = await AttendanceAsync(gym, gym.Saturday, gym.Saturday.AddDays(3), At(gym.Saturday.AddDays(3), 20, 0));

        attendance.Sessions.Single(row => row.Date == gym.Saturday).State.ShouldBe(SessionPresence.OnTime);
        attendance.Outside.ShouldBe(
        [
            new OutsideVisit(gym.Saturday, new VisitTimes(new TimeOnly(18, 0), new TimeOnly(19, 30), AutoCheckedOut: false)),
            new OutsideVisit(gym.Saturday.AddDays(1), new VisitTimes(new TimeOnly(10, 0), null, AutoCheckedOut: true)),
        ]);
    }

    [Fact]
    public async Task Attendance_CheckInBeforeTheStudentJoinedTheTrainer_IsNotListed()
    {
        var gym = await GymAsync();
        var yesterday = Today().AddDays(-1);
        await VisitAsync(gym.MemberId, At(yesterday, 10, 0));

        var attendance = await AttendanceAsync(gym, yesterday, gym.Saturday, At(gym.Saturday, 20, 0));

        attendance.Outside.ShouldBeEmpty();
    }

    [Fact]
    public async Task Attendance_DatesAfterToday_AreLeftOut()
    {
        var gym = await GymAsync();

        var attendance = await AttendanceAsync(gym, gym.Saturday, gym.Saturday.AddDays(20), At(gym.Saturday, 12, 0));

        attendance.Sessions.ShouldHaveSingleItem().State.ShouldBe(SessionPresence.NotYet);
        attendance.Totals.ShouldBe(new AttendanceTotals(0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Attendance_LeaveAndRemovedSession_AreCountedApart()
    {
        var gym = await GymAsync();
        using var left = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/leave", Place(gym.Saturday, 13), gym.MemberToken);
        left.StatusCode.ShouldBe(HttpStatusCode.OK);
        await SetDayAsync(gym, gym.Saturday.AddDays(2), Session(15, 2));

        var attendance = await AttendanceAsync(gym, gym.Saturday, gym.Saturday.AddDays(3), At(gym.Saturday.AddDays(3), 20, 0));

        attendance.Sessions.Select(row => row.State).ShouldBe([SessionPresence.Leave, SessionPresence.Cancelled]);
        attendance.Totals.ShouldBe(new AttendanceTotals(0, 0, 0, 0, Leave: 1, Cancelled: 1));
    }

    [Fact]
    public async Task GetAttendance_RangeLongerThan62Days_IsRefused()
    {
        var gym = await GymAsync();
        var from = Today();

        using var response = await gym.Client.SendAsync(
            Send(HttpMethod.Get, $"/api/site/coaching/students/{gym.MemberId}/attendance?from={Iso(from)}&to={Iso(from.AddDays(62))}", gym.TrainerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.AttendanceRangeInvalid.Code);
    }

    [Fact]
    public async Task GetAttendance_NotTheTrainersStudent_IsRefused()
    {
        var gym = await GymAsync();
        var other = await AddMemberAsync(OtherMemberPhone);
        var from = Today();

        using var response = await gym.Client.SendAsync(
            Send(HttpMethod.Get, $"/api/site/coaching/students/{other.Id}/attendance?from={Iso(from)}&to={Iso(from.AddDays(30))}", gym.TrainerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotAStudent.Code);
    }

    [Fact]
    public async Task GetAttendance_AsTheTrainer_ReturnsTheRange()
    {
        var gym = await GymAsync();
        var from = Today();

        using var response = await gym.Client.SendAsync(
            Send(HttpMethod.Get, $"/api/site/coaching/students/{gym.MemberId}/attendance?from={Iso(from)}&to={Iso(from.AddDays(30))}", gym.TrainerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("memberId").GetGuid().ShouldBe(gym.MemberId);
        body.GetProperty("totals").GetProperty("absent").GetInt32().ShouldBe(0);
    }

    // ---- Helpers ----

    private static DayStudent StudentAt(TrainerDayResponse day, int hour) =>
        day.Sessions.Single(row => !row.Removed && row.Start == new TimeOnly(hour, 0)).Students.ShouldHaveSingleItem();

    private Task<TrainerDayResponse> TodayAsync(Gym gym, DateTimeOffset at) => ReadDayAsync(gym, date: null, at);

    private Task<TrainerDayResponse> DayAsync(Gym gym, DateOnly date, DateTimeOffset at) => ReadDayAsync(gym, date, at);

    private async Task<TrainerDayResponse> ReadDayAsync(Gym gym, DateOnly? date, DateTimeOffset at)
    {
        await using var scope = Fixture.CreateScope();
        var handler = new GetTrainerDayHandler(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), CalendarAt(at));

        var result = await handler.Handle(gym.TrainerAccountId, date, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<StudentAttendanceResponse> AttendanceAsync(Gym gym, DateOnly from, DateOnly to, DateTimeOffset at)
    {
        await using var scope = Fixture.CreateScope();
        var handler = new GetStudentAttendanceHandler(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), CalendarAt(at));

        var result = await handler.Handle(gym.TrainerAccountId, gym.MemberId, from, to, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static GymCalendar CalendarAt(DateTimeOffset at) =>
        new(new FakeTimeProvider(at), Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

    /// <summary>A check-in at the desk on the member's one plan, each in a locker of its own; checked out, auto-closed or cancelled when asked.</summary>
    private async Task VisitAsync(
        Guid memberId, DateTimeOffset checkedInAt, DateTimeOffset? checkedOutAt = null, bool cancelled = false, DateTimeOffset? autoClosedAt = null)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!_plans.TryGetValue(memberId, out var planId))
        {
            var subscription = Subscription.CreateMembership(memberId, 12, 10_000m, Today()).Value;
            db.Subscriptions.Add(subscription);
            planId = _plans[memberId] = subscription.Id;
        }

        var visit = Attendance.CheckIn(memberId, planId, TestLockers.IdOf(++_lockerNumber), checkedInAt);
        if (checkedOutAt is { } outAt)
        {
            visit.CheckOut(outAt).IsSuccess.ShouldBeTrue();
        }

        if (autoClosedAt is { } closedAt)
        {
            visit.AutoClose(closedAt);
        }

        if (cancelled)
        {
            visit.Cancel(checkedInAt.AddMinutes(1), cancelWindowMinutes: 30).IsSuccess.ShouldBeTrue();
        }

        db.Attendances.Add(visit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Gym> GymAsync()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync();
        var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var tomorrow = Today().AddDays(1);
        var gym = new Gym(client, trainerToken, memberToken, trainer.Id, await TrainerAccountIdAsync(), member.Id, tomorrow, Next(DayOfWeek.Saturday, tomorrow));

        await LinkAsync(gym);
        await SetPatternAsync(gym, tomorrow, Weekly(DayOfWeek.Saturday, 13, 2), Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2));
        await SetScheduleAsync(gym, member.Id, Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13));

        return gym;
    }

    /// <summary>The member asks the trainer, and the trainer accepts.</summary>
    private static async Task LinkAsync(Gym gym)
    {
        var ask = Send(HttpMethod.Post, "/api/site/coaching/my-trainer/request", gym.MemberToken);
        ask.Content = JsonContent.Create(new { trainerId = gym.TrainerId });
        using var asked = await gym.Client.SendAsync(ask, TestContext.Current.CancellationToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestId = (await asked.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();

        using var accepted = await gym.Client.SendAsync(
            Send(HttpMethod.Post, $"/api/site/coaching/requests/{requestId}/accept", gym.TrainerToken), TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task SetPatternAsync(Gym gym, DateOnly startsOn, params object[] sessions)
    {
        using var response = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", new { startsOn = Iso(startsOn), sessions });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task SetDayAsync(Gym gym, DateOnly date, params object[] sessions)
    {
        using var response = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/timetable/days/{Iso(date)}", new { sessions });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The student's fixed schedule from tomorrow for four weeks.</summary>
    private static async Task SetScheduleAsync(Gym gym, Guid memberId, params object[] slots)
    {
        using var response = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/coaching/students/{memberId}/schedule", new
        {
            startsOn = Iso(gym.Tomorrow),
            endsOn = Iso(gym.Tomorrow.AddDays(27)),
            slots,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static object Weekly(DayOfWeek day, int hour, int capacity) =>
        new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", capacity };

    private static object Session(int hour, int capacity) => new { start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", capacity };

    private static object Slot(DayOfWeek day, int hour) => new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00" };

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

    private static HttpRequestMessage Send(HttpMethod method, string path, string token) => new HttpRequestMessage(method, path).WithBearer(token);

    /// <summary>Sends <paramref name="body"/> as the trainer, or as <paramref name="token"/>'s account.</summary>
    private static Task<HttpResponseMessage> SendJsonAsync(Gym gym, HttpMethod method, string path, object body, string? token = null)
    {
        var request = Send(method, path, token ?? gym.TrainerToken);
        request.Content = JsonContent.Create(body);

        return gym.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> SignInAsync(HttpClient client, string phone)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

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

    /// <summary>The site account the trainer signed in with: the handlers take the account, not the trainer.</summary>
    private async Task<Guid> TrainerAccountIdAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteAccounts.AsNoTracking()
            .Where(account => account.TrainerId != null)
            .Select(account => account.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private sealed record Gym(
        HttpClient Client,
        string TrainerToken,
        string MemberToken,
        Guid TrainerId,
        Guid TrainerAccountId,
        Guid MemberId,
        DateOnly Tomorrow,
        DateOnly Saturday);
}

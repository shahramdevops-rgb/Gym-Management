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

namespace Gym.Api.IntegrationTests.Bookings;

/// <summary>
/// Leave by request (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i): the student asks by
/// the day before, the trainer accepts (the place is freed) or declines (the student stays), and the
/// student may take a waiting request back, or an accepted one with «می‌آیم».
/// </summary>
/// <remarks>The clock is the real one, so the sessions are tomorrow or later; the session's start is the Domain tests'.</remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LeaveRequestTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string OtherTrainerPhone = "+989361234567";
    private const string MemberPhone = "+989121234567";
    private const string MySessionsPath = "/api/site/me/sessions";
    private const string LeaveRequestsPath = "/api/site/coaching/leave-requests";

    // ---- Asking ----

    [Fact]
    public async Task Leave_Asked_WaitsForTheTrainerAndTheStudentStaysIn()
    {
        var gym = await GymAsync();

        using var asked = await AskAsync(gym, gym.Saturday);

        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saturday = SessionOn(await asked.ReadJsonAsync(), gym.Saturday);
        saturday.GetProperty("leave").GetString().ShouldBe("Pending");
        saturday.GetProperty("left").GetBoolean().ShouldBeFalse();
        (await SkipsAsync()).ShouldBeEmpty();
        var requests = (await GetJsonAsync(gym.Client, LeaveRequestsPath, gym.TrainerToken)).GetProperty("requests");
        var request = requests.EnumerateArray().ShouldHaveSingleItem();
        request.GetProperty("memberId").GetGuid().ShouldBe(gym.MemberId);
        request.GetProperty("date").GetString().ShouldBe(Iso(gym.Saturday));
        request.GetProperty("start").GetString().ShouldBe("13:00:00");
    }

    [Fact]
    public async Task Leave_ForToday_Returns422LeaveTooLate()
    {
        var gym = await GymAsync();
        var today = await TodaysSessionAsync(gym);

        using var asked = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(today, 21), gym.MemberToken);

        asked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await asked.ReadErrorCodeAsync()).ShouldBe(BookingErrors.LeaveTooLate.Code);
        (await RequestsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_AskedTwice_Returns409LeaveAlreadyAsked()
    {
        var gym = await GymAsync();
        using var first = await AskAsync(gym, gym.Saturday);

        using var again = await AskAsync(gym, gym.Saturday);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadErrorCodeAsync()).ShouldBe(BookingErrors.LeaveAlreadyAsked.Code);
        (await RequestsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Leave_NotInThatSession_Returns404()
    {
        var gym = await GymAsync();

        using var asked = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 17), gym.MemberToken);

        asked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await asked.ReadErrorCodeAsync()).ShouldBe(BookingErrors.NotInSession.Code);
    }

    // ---- The trainer's answer ----

    [Fact]
    public async Task Accept_Waiting_TakesTheStudentOutAndFreesThePlace()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);

        using var accepted = await AnswerAsync(gym, await RequestIdAsync(gym), "accept");

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accepted.ReadJsonAsync()).GetProperty("requests").EnumerateArray().ShouldBeEmpty();
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(LeaveRequestStatus.Accepted);
        (await SkipsAsync()).ShouldHaveSingleItem().Reason.ShouldBe(SkipReason.Left);
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "13:00:00").ShouldBe(0);
        var saturday = SessionOn(await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken), gym.Saturday);
        saturday.GetProperty("left").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Decline_Waiting_TheStudentStaysInAndSeesItDeclined()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);

        using var declined = await AnswerAsync(gym, await RequestIdAsync(gym), "decline");

        declined.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(LeaveRequestStatus.Declined);
        (await SkipsAsync()).ShouldBeEmpty();
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "13:00:00").ShouldBe(1);
        SessionOn(await GetJsonAsync(gym.Client, MySessionsPath, gym.MemberToken), gym.Saturday)
            .GetProperty("leave").GetString().ShouldBe("Declined");
    }

    [Fact]
    public async Task Accept_AlreadyAnswered_Returns409LeaveNotWaiting()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var requestId = await RequestIdAsync(gym);
        using var declined = await AnswerAsync(gym, requestId, "decline");

        using var accepted = await AnswerAsync(gym, requestId, "accept");

        accepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(BookingErrors.LeaveNotWaiting.Code);
        (await SkipsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Accept_AnotherTrainersRequest_Returns404()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var requestId = await RequestIdAsync(gym);
        await AddTrainerAsync(OtherTrainerPhone);
        var otherToken = await SignInAsync(gym.Client, OtherTrainerPhone);

        using var accepted = await gym.Client.SendAsync(
            Send(HttpMethod.Post, $"{LeaveRequestsPath}/{requestId}/accept", otherToken), TestContext.Current.CancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(BookingErrors.LeaveRequestNotFound.Code);
    }

    [Fact]
    public async Task Accept_DateMovedMeanwhile_Returns404AndTheListLeavesItOut()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var requestId = await RequestIdAsync(gym);
        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);

        var requests = (await GetJsonAsync(gym.Client, LeaveRequestsPath, gym.TrainerToken)).GetProperty("requests");
        using var accepted = await AnswerAsync(gym, requestId, "accept");

        requests.EnumerateArray().ShouldBeEmpty();
        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accept_ADateMovedTo_RemovesThatBookingAndReadsAsLeave()
    {
        var gym = await GymAsync();
        using var moved = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/move", Move(gym.Saturday, 13, gym.Saturday, 17), gym.MemberToken);
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var asked = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(gym.Saturday, 17), gym.MemberToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var accepted = await AnswerAsync(gym, await RequestIdAsync(gym), "accept");

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BookingsAsync()).ShouldBeEmpty();
        Students(await GetJsonAsync(gym.Client, "/api/site/timetable", gym.TrainerToken), gym.Saturday, "17:00:00").ShouldBe(0);
    }

    // ---- The student taking it back ----

    [Fact]
    public async Task Cancel_Waiting_IsGoneFromTheTrainersList()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);

        using var cancelled = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave/cancel", Place(gym.Saturday, 13), gym.MemberToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionOn(await cancelled.ReadJsonAsync(), gym.Saturday).GetProperty("leave").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(LeaveRequestStatus.Cancelled);
        (await GetJsonAsync(gym.Client, LeaveRequestsPath, gym.TrainerToken)).GetProperty("requests").EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_NoneWaiting_Returns404()
    {
        var gym = await GymAsync();

        using var cancelled = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave/cancel", Place(gym.Saturday, 13), gym.MemberToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await cancelled.ReadErrorCodeAsync()).ShouldBe(BookingErrors.LeaveRequestNotFound.Code);
    }

    [Fact]
    public async Task Return_AfterAccepted_TakesTheRequestBackAndLeaveCanBeAskedAgain()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        using var accepted = await AnswerAsync(gym, await RequestIdAsync(gym), "accept");

        using var back = await SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/return", Place(gym.Saturday, 13), gym.MemberToken);
        using var again = await AskAsync(gym, gym.Saturday);

        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SkipsAsync()).ShouldBeEmpty();
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).Select(request => request.Status).Order()
            .ShouldBe([LeaveRequestStatus.Pending, LeaveRequestStatus.Cancelled]);
    }

    [Fact]
    public async Task Leave_AfterADecline_CanBeAskedAgain()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        using var declined = await AnswerAsync(gym, await RequestIdAsync(gym), "decline");

        using var again = await AskAsync(gym, gym.Saturday);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionOn(await again.ReadJsonAsync(), gym.Saturday).GetProperty("leave").GetString().ShouldBe("Pending");
    }

    // ---- The table holds it too ----

    [Fact]
    public async Task Insert_SecondLiveRequestForAPlace_RejectedByTheUniqueIndex()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var request = (await RequestsAsync()).Single();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(request, "Accepted", request.RequestedOn));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe("ux_leave_requests_one_live_per_place");
    }

    [Fact]
    public async Task Insert_DeclinedBesideAWaitingOne_Allowed()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var request = (await RequestsAsync()).Single();

        await InsertAsync(request, "Declined", request.RequestedOn);

        (await RequestsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Insert_AskedOnTheDateItself_RejectedByTheCheckConstraint()
    {
        var gym = await GymAsync();
        using var asked = await AskAsync(gym, gym.Saturday);
        var request = (await RequestsAsync()).Single();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(request, "Declined", request.Date));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_leave_requests_asked_before_the_date");
    }

    // ---- Helpers ----

    /// <summary>
    /// A trainer and their student, both signed in; the trainer's week from tomorrow (Saturday 13:00
    /// and 17:00), and the student in Saturday 13:00 for the next two weeks.
    /// </summary>
    private async Task<Gym> GymAsync()
    {
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync(TrainerPhone);
        var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var tomorrow = Today().AddDays(1);
        var gym = new Gym(client, trainerToken, memberToken, trainer.Id, member.Id, Next(DayOfWeek.Saturday, tomorrow));

        using var ask = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/coaching/my-trainer/request", new { trainerId = trainer.Id }, memberToken);
        ask.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestId = (await ask.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
        using var accepted = await client.SendAsync(
            Send(HttpMethod.Post, $"/api/site/coaching/requests/{requestId}/accept", trainerToken), TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var pattern = await SendJsonAsync(gym, HttpMethod.Put, "/api/site/timetable/pattern", new
        {
            startsOn = Iso(tomorrow),
            sessions = new[] { Weekly(DayOfWeek.Saturday, 13, 5), Weekly(DayOfWeek.Saturday, 17, 5) },
        });
        pattern.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var schedule = await SendJsonAsync(gym, HttpMethod.Put, $"/api/site/coaching/students/{member.Id}/schedule", new
        {
            startsOn = Iso(tomorrow),
            endsOn = Iso(tomorrow.AddDays(13)),
            slots = new[] { new { day = "Saturday", start = "13:00:00", end = "14:00:00" } },
        });
        schedule.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    /// <summary>
    /// Today with a session from 21:00 to 22:00 and the student in it by a fixed schedule from
    /// yesterday. Written straight to the database, whatever the hour the tests run.
    /// </summary>
    private async Task<DateOnly> TodaysSessionAsync(Gym gym)
    {
        var today = Today();
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkId = await db.TrainerLinks.Where(link => link.MemberId == gym.MemberId).Select(link => link.Id).SingleAsync(TestContext.Current.CancellationToken);
        var late = new PlannedSession(new TimeOnly(21, 0), new TimeOnly(22, 0), 5);
        db.TimetableDays.Add(TimetableDay.Create(gym.TrainerId, today, [], [late], today.AddDays(-1), new TimeOnly(12, 0)).Value);
        db.FixedSchedules.Add(FixedSchedule.Create(
            gym.TrainerId, gym.MemberId, linkId, today.AddDays(-1), today,
            [new WeeklySlot(today.DayOfWeek, late.Start, late.End)], today.AddDays(-1)).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return today;
    }

    private static Task<HttpResponseMessage> AskAsync(Gym gym, DateOnly date) =>
        SendJsonAsync(gym, HttpMethod.Post, $"{MySessionsPath}/leave", Place(date, 13), gym.MemberToken);

    private static async Task<Guid> RequestIdAsync(Gym gym) =>
        (await GetJsonAsync(gym.Client, LeaveRequestsPath, gym.TrainerToken)).GetProperty("requests")
            .EnumerateArray().Single().GetProperty("requestId").GetGuid();

    private static Task<HttpResponseMessage> AnswerAsync(Gym gym, Guid requestId, string answer) =>
        gym.Client.SendAsync(Send(HttpMethod.Post, $"{LeaveRequestsPath}/{requestId}/{answer}", gym.TrainerToken), TestContext.Current.CancellationToken);

    private static JsonElement SessionOn(JsonElement mySessions, DateOnly date) =>
        mySessions.GetProperty("sessions").EnumerateArray().Single(session => session.GetProperty("date").GetString() == Iso(date));

    /// <summary>The students of the session of <paramref name="date"/> starting at <paramref name="start"/>, in «تمام سانس‌های من».</summary>
    private static int Students(JsonElement timetable, DateOnly date, string start) =>
        timetable.GetProperty("days").EnumerateArray()
            .Single(day => day.GetProperty("date").GetString() == Iso(date))
            .GetProperty("sessions").EnumerateArray()
            .Single(session => session.GetProperty("start").GetString() == start)
            .GetProperty("students").GetInt32();

    private async Task InsertAsync(LeaveRequest like, string status, DateOnly requestedOn)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""
            INSERT INTO leave_requests (id, trainer_id, member_id, link_id, date, start_time, end_time, status, requested_on, created_at)
            VALUES ({Guid.CreateVersion7()}, {like.TrainerId}, {like.MemberId}, {like.LinkId}, {like.Date}, {like.Start}, {like.End}, {status}, {requestedOn}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private static object Weekly(DayOfWeek day, int hour, int capacity) =>
        new { day = day.ToString(), start = $"{hour:00}:00:00", end = $"{hour + 1:00}:00:00", capacity };

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

    private async Task<Trainer> AddTrainerAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = Trainer.Create("رضا محمدی", phone, new DateOnly(2026, 10, 1)).Value;
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return trainer;
    }

    private async Task<List<LeaveRequest>> RequestsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().LeaveRequests.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
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

    private sealed record Gym(HttpClient Client, string TrainerToken, string MemberToken, Guid TrainerId, Guid MemberId, DateOnly Saturday);
}

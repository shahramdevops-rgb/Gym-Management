using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.Notifications;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendSessionCancelledSms;
using Gym.Domain.Bookings;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Bookings;

/// <summary>
/// The trainer cancelling a session its students are in (BUSINESS_RULES.md §15 <i>Timetable and
/// attendance</i>, task 15.6c): what a save of the timetable records, the notice on the member's
/// home and «دیدم», and the student's SMS, asked for at the save and sent by its job on a clock each
/// test chooses.
/// </summary>
/// <remarks>
/// The API's clock is the real one, so the sessions are tomorrow or later, where the time of day
/// plays no part: the first Saturday after today is always within the SMS's 7 days.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SessionCancellationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainerPhone = "+989351234567";
    private const string MemberPhone = "+989121234567";
    private const string OtherMemberPhone = "+989121234568";

    private const string CancelledPath = "/api/site/me/sessions/cancelled";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    // ---- What a save records ----

    [Fact]
    public async Task SetDay_RemovesASessionWithAStudent_RecordsItAndAsksForTheSms()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        var scheduler = Scheduler();

        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        var row = (await CancellationsAsync()).ShouldHaveSingleItem();
        (row.MemberId, row.TrainerId, row.Date, row.Start).ShouldBe((gym.MemberId, gym.TrainerId, gym.Saturday, new TimeOnly(13, 0)));
        scheduler.Asked.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            asked => asked.ChangeId.ShouldBe(row.ChangeId),
            asked => asked.MemberId.ShouldBe(gym.MemberId));
    }

    [Fact]
    public async Task SetDay_MovesTheSessionsTimes_CancelsIt()
    {
        var gym = await GymAsync();

        await SetDayAsync(gym, gym.Saturday, Session(14, 2), Session(17, 5));

        (await CancellationsAsync()).ShouldHaveSingleItem().Start.ShouldBe(new TimeOnly(13, 0));
    }

    [Fact]
    public async Task SetDay_LowersTheCapacityBelowItsStudents_CancelsNothing()
    {
        var gym = await GymAsync();
        var other = await AddStudentAsync(gym, OtherMemberPhone);
        await SetScheduleAsync(gym, other, Slot(DayOfWeek.Saturday, 13));

        await SetDayAsync(gym, gym.Saturday, Session(13, 1), Session(17, 5));

        (await CancellationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDay_SmsOff_RecordsItAndAsksForNothing()
    {
        await TurnOnAsync(sessionCancelled: false);
        var gym = await GymAsync();
        var scheduler = Scheduler();

        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        (await CancellationsAsync()).ShouldHaveSingleItem();
        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPattern_DropsTheSlot_CancelsEveryDateUnderOneSaveAndAsksOnce()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        var scheduler = Scheduler();

        await SetPatternAsync(gym, gym.Tomorrow, Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2));

        var rows = await CancellationsAsync();
        rows.Select(row => row.Date).ShouldBe([gym.Saturday, gym.Saturday.AddDays(7), gym.Saturday.AddDays(14), gym.Saturday.AddDays(21)], ignoreOrder: true);
        rows.Select(row => row.ChangeId).Distinct().ShouldHaveSingleItem();
        scheduler.Asked.ShouldHaveSingleItem().MemberId.ShouldBe(gym.MemberId);
    }

    [Fact]
    public async Task SetPattern_OnlyDatesPastSevenDays_RecordsThemWithNoSms()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        var scheduler = Scheduler();
        var later = gym.Saturday.AddDays(14);

        await SetPatternAsync(gym, later, Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2));

        (await CancellationsAsync()).Select(row => row.Date).ShouldBe([later, later.AddDays(7)], ignoreOrder: true);
        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetDay_TakesAwayTheSessionAMemberMovedTo_CancelsIt()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(13, 2), Session(15, 2), Session(17, 5));
        using var moved = await SendJsonAsync(gym, HttpMethod.Post, "/api/site/me/sessions/move", Move(gym.Saturday, 13, gym.Saturday, 15), gym.MemberToken);
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var reset = await gym.Client.SendAsync(Send(HttpMethod.Delete, $"/api/site/timetable/days/{Iso(gym.Saturday)}", gym.TrainerToken), TestContext.Current.CancellationToken);

        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CancellationsAsync()).ShouldHaveSingleItem().Start.ShouldBe(new TimeOnly(15, 0));
    }

    [Fact]
    public async Task ResetDay_BringsTheSessionBack_CancelsNothingAndClearsTheNotice()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        using var reset = await gym.Client.SendAsync(Send(HttpMethod.Delete, $"/api/site/timetable/days/{Iso(gym.Saturday)}", gym.TrainerToken), TestContext.Current.CancellationToken);

        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CancellationsAsync()).ShouldHaveSingleItem();
        (await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken)).GetProperty("sessions").GetArrayLength().ShouldBe(0);
    }

    // ---- The notice on the member's home ----

    [Fact]
    public async Task GetCancelled_AfterASave_ListsTheSessionWithItsLatest()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        var body = await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken);

        var session = body.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
        session.GetProperty("date").GetString().ShouldBe(Iso(gym.Saturday));
        session.GetProperty("start").GetString().ShouldBe("13:00:00");
        body.GetProperty("latest").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task GetCancelled_CancelledTwice_ListsItOnce()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        await SetDayAsync(gym, gym.Saturday, Session(13, 2), Session(17, 5));
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));

        var body = await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken);

        (await CancellationsAsync()).Count.ShouldBe(2);
        body.GetProperty("sessions").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task GetCancelled_NothingCancelled_IsEmpty()
    {
        var gym = await GymAsync();

        var body = await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken);

        body.GetProperty("sessions").GetArrayLength().ShouldBe(0);
        body.GetProperty("latest").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetCancelled_AsTheTrainer_Returns403()
    {
        var gym = await GymAsync();

        using var response = await gym.Client.SendAsync(Send(HttpMethod.Get, CancelledPath, gym.TrainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Seen_UpToTheLatest_ClearsTheNotice()
    {
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var latest = (await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken)).GetProperty("latest").GetString();

        using var seen = await SendJsonAsync(gym, HttpMethod.Post, $"{CancelledPath}/seen", new { upTo = latest }, gym.MemberToken);

        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await seen.ReadJsonAsync()).GetProperty("sessions").GetArrayLength().ShouldBe(0);
        (await CancellationsAsync()).ShouldHaveSingleItem().SeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Seen_ACancellationSavedAfterTheRead_StaysToBeSeen()
    {
        var gym = await GymAsync();
        await SetScheduleAsync(gym, gym.MemberId, Slot(DayOfWeek.Saturday, 13), Slot(DayOfWeek.Monday, 13));
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var latest = (await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken)).GetProperty("latest").GetString();
        await SetDayAsync(gym, gym.Monday);

        using var seen = await SendJsonAsync(gym, HttpMethod.Post, $"{CancelledPath}/seen", new { upTo = latest }, gym.MemberToken);

        var left = (await seen.ReadJsonAsync()).GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
        left.GetProperty("date").GetString().ShouldBe(Iso(gym.Monday));
    }

    // ---- The SMS job ----

    [Fact]
    public async Task Handle_OneSessionCancelled_SendsItsTextToTheMember()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;

        var outcome = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));

        outcome.ShouldBe(SessionCancelledSmsOutcome.Sent);
        var sent = (await NotificationsAsync()).ShouldHaveSingleItem();
        sent.Kind.ShouldBe(NotificationKind.SessionCancelled);
        sent.Recipient.ShouldBe(MemberPhone);
        sent.TimetableChangeId.ShouldBe(changeId);
        sent.Text.ShouldBe(SmsText.ForSessionCancelled("علی کریمی", "رضا محمدی", gym.Saturday, new TimeOnly(13, 0), others: 0));
    }

    [Fact]
    public async Task Handle_APatternThatCancelledFourDates_NamesTheNearestAndCountsTheOthers()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetPatternAsync(gym, gym.Tomorrow, Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2));
        var changeId = (await CancellationsAsync()).First().ChangeId;

        await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));

        (await NotificationsAsync()).ShouldHaveSingleItem().Text
            .ShouldBe(SmsText.ForSessionCancelled("علی کریمی", "رضا محمدی", gym.Saturday, new TimeOnly(13, 0), others: 3));
    }

    [Fact]
    public async Task Handle_SessionBroughtBack_SendsNothing()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;
        await SetDayAsync(gym, gym.Saturday, Session(13, 2), Session(17, 5));

        var outcome = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));

        outcome.ShouldBe(SessionCancelledSmsOutcome.NoLongerDue);
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_SeenInTheAppFirst_SendsNothing()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;
        var latest = (await GetJsonAsync(gym.Client, CancelledPath, gym.MemberToken)).GetProperty("latest").GetString();
        using var seen = await SendJsonAsync(gym, HttpMethod.Post, $"{CancelledPath}/seen", new { upTo = latest }, gym.MemberToken);
        seen.StatusCode.ShouldBe(HttpStatusCode.OK);

        var outcome = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));

        outcome.ShouldBe(SessionCancelledSmsOutcome.NoLongerDue);
    }

    [Fact]
    public async Task Handle_TurnedOffSinceTheSave_SendsNothing()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;
        await TurnOnAsync(sessionCancelled: false);

        var outcome = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));

        outcome.ShouldBe(SessionCancelledSmsOutcome.Off);
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RunTwice_SendsOnce()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;

        await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 0));
        var second = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 12, 5));

        second.ShouldBe(SessionCancelledSmsOutcome.AlreadyWritten);
        (await NotificationsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_At2300_WaitsForEightTheNextMorning()
    {
        await TurnOnAsync();
        var gym = await GymAsync();
        await SetDayAsync(gym, gym.Saturday, Session(17, 5));
        var changeId = (await CancellationsAsync()).Single().ChangeId;
        var scheduler = Scheduler();

        var outcome = await HandleAsync(changeId, gym.MemberId, new ScriptedSmsSender(), At(Today(), 23, 0));

        outcome.ShouldBe(SessionCancelledSmsOutcome.Deferred);
        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBe(At(Today().AddDays(1), 8, 0));
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Database_SecondSmsForTheSameMemberAndSave_IsRefused()
    {
        var gym = await GymAsync();
        var changeId = Guid.CreateVersion7();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForSessionCancelled(gym.MemberId, changeId, MemberPhone, "متن"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Notifications.Add(Notification.ForSessionCancelled(gym.MemberId, changeId, MemberPhone, "متن"));

        var refused = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        refused.ConstraintName.ShouldBe(NotificationConstraints.OneSessionCancelledPerMemberAndChange);
    }

    // ---- Helpers ----

    /// <summary>
    /// A trainer with Saturday 13:00 (room for 2) and 17:00, and Monday 13:00, from tomorrow, and the
    /// member their student, in every Saturday 13:00 of the next four weeks.
    /// </summary>
    private async Task<Gym> GymAsync()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync();
        var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var tomorrow = Today().AddDays(1);
        var gym = new Gym(client, trainerToken, memberToken, trainer.Id, member.Id, tomorrow, Next(DayOfWeek.Saturday, tomorrow), Next(DayOfWeek.Monday, tomorrow));

        await LinkAsync(gym, memberToken);
        await SetPatternAsync(gym, tomorrow, Weekly(DayOfWeek.Saturday, 13, 2), Weekly(DayOfWeek.Saturday, 17, 5), Weekly(DayOfWeek.Monday, 13, 2));
        await SetScheduleAsync(gym, member.Id, Slot(DayOfWeek.Saturday, 13));

        return gym;
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

    private async Task<SessionCancelledSmsOutcome> HandleAsync(Guid changeId, Guid memberId, ScriptedSmsSender sender, DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendSessionCancelledSmsHandler(
            db,
            calendar,
            time,
            sender,
            NoWait,
            new SessionCancelledSms(db, calendar, Scheduler(clear: false), NullLogger<SessionCancelledSms>.Instance),
            NullLogger<SendSessionCancelledSmsHandler>.Instance);

        return await handler.Handle(changeId, memberId, TestContext.Current.CancellationToken);
    }

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingSessionCancelledSmsScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<ISessionCancelledSmsScheduler>()
            .ShouldBeOfType<RecordingSessionCancelledSmsScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    /// <summary>All SMS and the cancelled session's SMS on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool sessionCancelled = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            true,
            SmsKindSettings.Off,
            LowSessionsSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            welcomeBackEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false,
            trainerRequestEnabled: false,
            sessionCancelledEnabled: sessionCancelled, coachingPeriodEnding: SmsSettings.CoachingPeriodEndingAtStart).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    private async Task<List<SessionCancellation>> CancellationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionCancellations.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
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
        Guid MemberId,
        DateOnly Tomorrow,
        DateOnly Saturday,
        DateOnly Monday);
}

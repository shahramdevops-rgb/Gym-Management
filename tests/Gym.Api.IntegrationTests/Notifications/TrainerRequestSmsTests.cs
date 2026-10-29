using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendTrainerRequestSms;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The trainer's SMS when a member picks them on the site (BUSINESS_RULES.md §15 <i>A member and
/// their trainer</i>, task 15.4c). The request goes through the API, which asks the test host's
/// recording scheduler; the job is then made as Hangfire would make it, on a clock each test chooses.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TrainerRequestSmsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string TrainerPhone = "+989351234567";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    // ---- Asked for by the request ----

    [Fact]
    public async Task Request_SmsOn_AsksForTheTrainersSms()
    {
        await TurnOnAsync();
        await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        var scheduler = Scheduler();

        var requestId = await RequestIdAsync(client, token, trainer.Id);

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(requestId);
    }

    [Fact]
    public async Task Request_TrainersSmsOff_AsksForNothing()
    {
        await TurnOnAsync(trainerRequest: false);
        await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        var scheduler = Scheduler();

        await RequestIdAsync(client, token, trainer.Id);

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_SameTrainerPressedTwice_AsksOnce()
    {
        await TurnOnAsync();
        await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        var scheduler = Scheduler();

        await RequestIdAsync(client, token, trainer.Id);
        await RequestIdAsync(client, token, trainer.Id);

        scheduler.Asked.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Queue_After22_WaitsFor10TheNextMorning()
    {
        var day = new DateOnly(2026, 10, 29);
        var requestId = Guid.CreateVersion7();
        var scheduler = Scheduler();
        await using var scope = Fixture.CreateScope();

        Queue(scope, At(day, 22, 30)).Queue(requestId);

        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((requestId, (DateTimeOffset?)At(day.AddDays(1), 10, 0)));
    }

    // ---- The job ----

    [Fact]
    public async Task Handle_RequestWaiting_SendsTheTrainerTheMembersName()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var request = await AddRequestAsync(member.Id, trainer.Id);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(request.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(TrainerRequestSmsOutcome.Sent);
        var sent = sender.Requests.ShouldHaveSingleItem();
        sent.Receptor.ShouldBe(TrainerPhone);
        sent.Text.ShouldBe(SmsText.ForTrainerRequest("رضا محمدی", "علی کریمی"));
        var row = (await NotificationsAsync()).ShouldHaveSingleItem();
        row.Kind.ShouldBe(NotificationKind.TrainerRequest);
        row.MemberId.ShouldBe(member.Id);
        row.TrainerId.ShouldBe(trainer.Id);
        row.ReportDate.ShouldBe(request.RequestedOn);
        row.Status.ShouldBe(NotificationStatus.Sent);
    }

    [Fact]
    public async Task Handle_RequestTakenBackBeforeItWent_SendsNothing()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var request = await AddRequestAsync(member.Id, trainer.Id);
        await CancelAsync(request.Id);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(request.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(TrainerRequestSmsOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_SecondRequestToTheSameTrainerThatDay_ReachesTheAppOnly()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var first = await AddRequestAsync(member.Id, trainer.Id);
        (await HandleAsync(first.Id, new ScriptedSmsSender(), At(Today(), 12, 0))).ShouldBe(TrainerRequestSmsOutcome.Sent);
        await CancelAsync(first.Id);
        var second = await AddRequestAsync(member.Id, trainer.Id, madeToday: 1);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(second.Id, sender, At(Today(), 12, 5));

        outcome.ShouldBe(TrainerRequestSmsOutcome.AlreadyWritten);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_TurnedOffSinceTheRequest_SendsNothing()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var request = await AddRequestAsync(member.Id, trainer.Id);
        await TurnOnAsync(trainerRequest: false);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(request.Id, sender, At(Today(), 12, 0));

        outcome.ShouldBe(TrainerRequestSmsOutcome.Off);
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RunAtNight_WaitsFor10()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var request = await AddRequestAsync(member.Id, trainer.Id);
        var scheduler = Scheduler();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(request.Id, sender, At(Today(), 23, 0));

        outcome.ShouldBe(TrainerRequestSmsOutcome.Deferred);
        sender.Requests.ShouldBeEmpty();
        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBe(At(Today().AddDays(1), 10, 0));
    }

    [Fact]
    public async Task Database_SecondTrainerSmsForTheSameMemberTrainerAndDay_IsRefused()
    {
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var day = Today();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForTrainerRequest(member.Id, trainer.Id, day, TrainerPhone, "متن"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Notifications.Add(Notification.ForTrainerRequest(member.Id, trainer.Id, day, TrainerPhone, "متن"));

        var refused = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        refused.ConstraintName.ShouldBe(NotificationConstraints.OneTrainerRequestPerMemberTrainerAndDay);
    }

    // ---- In the history ----

    [Fact]
    public async Task History_TrainersSms_IsNamedForTheTrainerWithNoMemberLink()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync();
        var trainer = await AddTrainerAsync();
        var request = await AddRequestAsync(member.Id, trainer.Id);
        await HandleAsync(request.Id, new ScriptedSmsSender(), At(Today(), 12, 0));
        var (client, token) = await OwnerClientAsync();

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/sms/messages?kind=TrainerRequest").WithBearer(token),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = (await response.ReadJsonAsync()).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("memberName").GetString().ShouldBe("رضا محمدی");
        row.GetProperty("memberId").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("recipient").GetString().ShouldBe(TrainerPhone);
    }

    // ---- Helpers ----

    private async Task<TrainerRequestSmsOutcome> HandleAsync(Guid requestId, ScriptedSmsSender sender, DateTimeOffset at)
    {
        var (time, calendar) = Clock(at);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendTrainerRequestSmsHandler(
            db,
            calendar,
            time,
            sender,
            NoWait,
            new TrainerRequestSms(db, calendar, Scheduler(clear: false), NullLogger<TrainerRequestSms>.Instance),
            NullLogger<SendTrainerRequestSmsHandler>.Instance);

        return await handler.Handle(requestId, TestContext.Current.CancellationToken);
    }

    private TrainerRequestSms Queue(AsyncServiceScope scope, DateTimeOffset at)
    {
        var (_, calendar) = Clock(at);

        return new TrainerRequestSms(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            calendar,
            Scheduler(clear: false),
            NullLogger<TrainerRequestSms>.Instance);
    }

    private static (FakeTimeProvider Time, GymCalendar Calendar) Clock(DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);

        return (time, new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })));
    }

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingTrainerRequestSmsScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<ITrainerRequestSmsScheduler>()
            .ShouldBeOfType<RecordingTrainerRequestSmsScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    /// <summary>All SMS and the trainer's SMS on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool trainerRequest = true)
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
            trainerRequestEnabled: trainerRequest).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private async Task<string> SignInAsync(HttpClient client, string phone)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> RequestIdAsync(HttpClient client, string token, Guid trainerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/site/coaching/my-trainer/request").WithBearer(token);
        request.Content = JsonContent.Create(new { trainerId });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

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

    /// <summary>A request waiting, made today, as the site's «انتخاب این مربی» saves it.</summary>
    private async Task<TrainerRequest> AddRequestAsync(Guid memberId, Guid trainerId, int madeToday = 0)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = TrainerRequest.Create(memberId, trainerId, Today(), madeToday).Value;
        db.TrainerRequests.Add(request);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return request;
    }

    private async Task CancelAsync(Guid requestId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.TrainerRequests.SingleAsync(row => row.Id == requestId, TestContext.Current.CancellationToken);
        request.Cancel().IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}

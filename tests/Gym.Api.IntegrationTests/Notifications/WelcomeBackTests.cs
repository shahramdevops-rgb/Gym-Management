using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendWelcomeBack;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Subscriptions;
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
/// The member's welcome back on their first visit after the absence SMS (BUSINESS_RULES.md §10
/// <i>The welcome back</i>, task 10.20).
/// </summary>
/// <remarks>
/// Each case turns on moments between the absence SMS, the visits and the job, so the handlers run on
/// a <see cref="FakeTimeProvider"/> on Monday 2026-10-26 in Tehran, and the messages, plans and visits
/// are written straight into the tables. The two check-ins' wiring goes through the API, on the real
/// clock.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class WelcomeBackTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string AbsenceText = "علی عزیز، جات توی باشگاه پاسارگاد خالیه!";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Today = new(2026, 10, 26);

    /// <summary>The day the members' absences count from.</summary>
    private static readonly DateOnly AbsentSince = Today.AddDays(-20);

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Asked for by the check-in ----

    [Fact]
    public async Task Queue_FirstVisitAfterTheAbsenceSms_AsksForItHalfAnHourLater()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((visit, (DateTimeOffset?)At(Today, 17, 40)));
    }

    [Fact]
    public async Task Queue_SecondVisitAfterTheAbsenceSms_AsksForNothing()
    {
        // The member came back before the release; this is the next visit, after it.
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-10), 11, 30));
        await AddVisitAsync(holder, At(Today.AddDays(-8), 18, 0));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queue_EarlierVisitCancelled_StillTheFirstVisit()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-10), 11, 30));
        await AddVisitAsync(holder, At(Today.AddDays(-8), 18, 0), cancelled: true);
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(visit);
    }

    [Fact]
    public async Task Queue_CardioOnlyVisit_CountsAsAVisit()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true, cardioOnly: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldHaveSingleItem().SubscriptionId.ShouldBe(visit);
    }

    [Fact]
    public async Task Queue_NoAbsenceSms_AsksForNothing()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(NotificationStatus.Failed, null)]
    [InlineData(NotificationStatus.Sent, SmsDelivery.Cancelled)]
    [InlineData(NotificationStatus.Sent, SmsDelivery.BlockedByReceiver)]
    public async Task Queue_AbsenceSmsNeverReachedThePhone_AsksForNothing(NotificationStatus status, SmsDelivery? delivery)
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30), status, delivery);
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queue_HalfAnHourBefore0800_AsksForIt0800()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 7, 0), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 7, 0));

        scheduler.Asked.ShouldHaveSingleItem().At.ShouldBe(At(Today, 8, 0));
    }

    [Fact]
    public async Task Queue_CheckInAt2340_AsksForNothing()
    {
        // Half an hour later is the next day: it would no longer be about coming back today.
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 23, 40), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 23, 40));

        scheduler.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Queue_AllSmsOrWelcomeBackOff_AsksForNothing(bool allSms, bool welcomeBack)
    {
        await TurnOnAsync(allSms, welcomeBack);
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var scheduler = Scheduler();

        await QueueAsync(visit, At(Today, 17, 10));

        scheduler.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("check-in")]
    [InlineData("cardio-only-check-in")]
    public async Task CheckIn_FirstVisitAfterTheAbsenceSms_AsksForItAfterTheVisitIsSaved(string path)
    {
        await TurnOnAsync();
        var (client, token) = await OwnerClientAsync();
        var plan = await TestPlans.AddAsync(Fixture);
        var member = await AddMemberAsync("سارا محمدی");
        using (var assigned = await SendAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body))
        {
            assigned.IsSuccessStatusCode.ShouldBeTrue();
        }

        await AddAbsenceSmsAsync(member.Id, DateTimeOffset.UtcNow.AddDays(-1));
        var scheduler = Scheduler();

        using var response = await SendAsync(
            client, token, $"/api/members/{member.Id}/attendance/{path}", new { lockerId = TestLockers.IdOf(3) });
        response.IsSuccessStatusCode.ShouldBeTrue();

        // On the real clock: nothing is asked for a check-in made after 23:30 in Tehran.
        var visit = await LatestVisitAsync(member.Id);
        var local = TimeZoneInfo.ConvertTime(visit.CheckedInAt, Tehran).DateTime;
        if (SmsSettings.WelcomeBackAt(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local)) is { } at)
        {
            scheduler.Asked.ShouldHaveSingleItem().ShouldBe(
                (visit.Id, (DateTimeOffset?)new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(at, Tehran), TimeSpan.Zero)));
        }
        else
        {
            scheduler.Asked.ShouldBeEmpty();
        }
    }

    // ---- Sent by the job ----

    [Fact]
    public async Task Handle_FirstWelcomeBack_SendsTheFirstTextToTheMember()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("سید علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(visit, sender, At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.Sent);
        var sms = (await WelcomeBacksAsync(holder.Member.Id)).ShouldHaveSingleItem();
        sms.Status.ShouldBe(NotificationStatus.Sent);
        sms.AbsentSince.ShouldBe(AbsentSince);
        sms.Recipient.ShouldBe(holder.Member.PhoneNumber);
        sms.Text.ShouldBe(SmsText.ForWelcomeBack("سید علی رضایی", turn: 0));
        sender.Requests.ShouldHaveSingleItem().Text.ShouldStartWith("علی عزیز، از اینکه به باشگاه پاسارگاد برگشتی");
    }

    [Fact]
    public async Task Handle_ThirdWelcomeBack_SendsTheThirdText()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddWelcomeBackAsync(holder.Member.Id, Today.AddDays(-90));
        await AddWelcomeBackAsync(holder.Member.Id, Today.AddDays(-50));
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);

        var outcome = await HandleAsync(visit, new ScriptedSmsSender(), At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.Sent);
        (await WelcomeBacksAsync(holder.Member.Id)).Single(sms => sms.AbsentSince == AbsentSince)
            .Text.ShouldBe(SmsText.ForWelcomeBack("علی رضایی", turn: 2));
    }

    [Fact]
    public async Task Handle_CheckInCancelledSince_SendsNothing()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), cancelled: true);
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(visit, sender, At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.NoLongerDue);
        sender.Requests.ShouldBeEmpty();
        (await WelcomeBacksAsync(holder.Member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CheckInCancelledAndMadeAgain_OnlyTheNewOneSends()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var cancelled = await AddVisitAsync(holder, At(Today, 17, 10), cancelled: true);
        var again = await AddVisitAsync(holder, At(Today, 17, 15), open: true);

        (await HandleAsync(cancelled, new ScriptedSmsSender(), At(Today, 17, 40))).ShouldBe(WelcomeBackOutcome.NoLongerDue);
        (await HandleAsync(again, new ScriptedSmsSender(), At(Today, 17, 45))).ShouldBe(WelcomeBackOutcome.Sent);

        (await WelcomeBacksAsync(holder.Member.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_WelcomeBackTurnedOffSinceTheCheckIn_SendsNothing()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        await TurnOnAsync(welcomeBack: false);

        var outcome = await HandleAsync(visit, new ScriptedSmsSender(), At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.Off);
        (await WelcomeBacksAsync(holder.Member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MemberDeactivatedSinceTheCheckIn_SendsNothing()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10));
        await DeactivateAsync(holder.Member.Id);

        var outcome = await HandleAsync(visit, new ScriptedSmsSender(), At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.NoLongerDue);
    }

    [Fact]
    public async Task Handle_MadeTwice_SendsOne()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var sender = new ScriptedSmsSender();

        var first = await HandleAsync(visit, sender, At(Today, 17, 40));
        var second = await HandleAsync(visit, sender, At(Today, 17, 41));

        first.ShouldBe(WelcomeBackOutcome.Sent);
        second.ShouldBe(WelcomeBackOutcome.AlreadyWritten);
        sender.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_RowLeftPendingByAStoppedJob_IsMarkedUnknownAndNotSent()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        await SaveAsync(Notification.ForWelcomeBack(holder.Member.Id, AbsentSince, holder.Member.PhoneNumber, "متن"));
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(visit, sender, At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.Interrupted);
        sender.Requests.ShouldBeEmpty();
        (await WelcomeBacksAsync(holder.Member.Id)).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Unknown);
    }

    [Fact]
    public async Task Handle_At2310_IsSentBeforeMidnight()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 22, 40), open: true);

        var outcome = await HandleAsync(visit, new ScriptedSmsSender(), At(Today, 23, 10));

        outcome.ShouldBe(WelcomeBackOutcome.Sent);
    }

    [Fact]
    public async Task Handle_TheNextDay_IsTooLateAndSendsNothing()
    {
        // The server was down at 17:40 and the job runs the next morning.
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10));
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(visit, sender, At(Today.AddDays(1), 9, 0));

        outcome.ShouldBe(WelcomeBackOutcome.TooLate);
        sender.Requests.ShouldBeEmpty();
        (await WelcomeBacksAsync(holder.Member.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Before0800_IsPutOffTo0800()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 6, 50), open: true);
        var scheduler = Scheduler();
        var sender = new ScriptedSmsSender();

        var outcome = await HandleAsync(visit, sender, At(Today, 7, 20));

        outcome.ShouldBe(WelcomeBackOutcome.Deferred);
        sender.Requests.ShouldBeEmpty();
        scheduler.Asked.ShouldHaveSingleItem().ShouldBe((visit, (DateTimeOffset?)At(Today, 8, 0)));
    }

    [Fact]
    public async Task Handle_FailureThatMayPass_IsTriedAgainAndSent()
    {
        await TurnOnAsync();
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));
        var visit = await AddVisitAsync(holder, At(Today, 17, 10), open: true);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var outcome = await HandleAsync(visit, sender, At(Today, 17, 40));

        outcome.ShouldBe(WelcomeBackOutcome.Sent);
        sender.Requests.Count.ShouldBe(2);
        (await WelcomeBacksAsync(holder.Member.Id)).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    // ---- The database ----

    [Fact]
    public async Task Save_SecondWelcomeBackForOneAbsence_RejectedByTheUniqueIndex()
    {
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddWelcomeBackAsync(holder.Member.Id, AbsentSince);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForWelcomeBack(holder.Member.Id, AbsentSince, holder.Member.PhoneNumber, "متن"));

        var exception = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        exception.ConstraintName.ShouldBe(NotificationConstraints.OneWelcomeBackPerMemberAndAbsence);
    }

    [Fact]
    public async Task Save_WelcomeBackBesideTheAbsenceSmsOfTheSameAbsence_IsAllowed()
    {
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddAbsenceSmsAsync(holder.Member.Id, At(Today.AddDays(-3), 11, 30));

        await AddWelcomeBackAsync(holder.Member.Id, AbsentSince);

        (await WelcomeBacksAsync(holder.Member.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Save_WelcomeBackWithoutItsAbsence_RejectedByACheckConstraint()
    {
        var holder = await AddMemberWithPlanAsync("علی رضایی");
        await AddWelcomeBackAsync(holder.Member.Id, AbsentSince);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exception = await Should.ThrowAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE notifications SET absent_since = NULL WHERE kind = 'WelcomeBack'",
            TestContext.Current.CancellationToken));
        exception.ConstraintName.ShouldBe(NotificationConstraints.EventMatchesKind);
    }

    // ---- Helpers ----

    private async Task QueueAsync(Guid attendanceId, DateTimeOffset at)
    {
        var (_, calendar) = Clock(at);

        await using var scope = Fixture.CreateScope();
        var welcomeBacks = new WelcomeBacks(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            calendar,
            Scheduler(clear: false),
            NullLogger<WelcomeBacks>.Instance);

        await welcomeBacks.QueueIfFirstVisitAsync(attendanceId, TestContext.Current.CancellationToken);
    }

    private async Task<WelcomeBackOutcome> HandleAsync(Guid attendanceId, ScriptedSmsSender sender, DateTimeOffset at)
    {
        var (time, calendar) = Clock(at);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendWelcomeBackHandler(
            db,
            calendar,
            time,
            sender,
            NoWait,
            new WelcomeBacks(db, calendar, Scheduler(clear: false), NullLogger<WelcomeBacks>.Instance),
            NullLogger<SendWelcomeBackHandler>.Instance);

        return await handler.Handle(attendanceId, TestContext.Current.CancellationToken);
    }

    private static (FakeTimeProvider Time, GymCalendar Calendar) Clock(DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);

        return (time, new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })));
    }

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingWelcomeBackScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<IWelcomeBackScheduler>()
            .ShouldBeOfType<RecordingWelcomeBackScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    /// <summary>All SMS and the welcome back on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool allSms = true, bool welcomeBack = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            allSms,
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
            welcomeBackEnabled: welcomeBack,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false, trainerRequestEnabled: false).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private static string Phone() => $"+98916{Interlocked.Increment(ref _phoneSuffix):D7}";

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var member = TestMembers.Seed(fullName, Phone());

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>An active member with a 30-session plan from 50 days before <see cref="Today"/> to 30 days after it.</summary>
    private async Task<(Member Member, Guid PlanId)> AddMemberWithPlanAsync(string fullName)
    {
        var member = await AddMemberAsync(fullName);
        var plan = Subscription.CreateMembership(member.Id, sessionCount: 30, sessionPrice: 90_000m, Today.AddDays(-50)).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET end_date = {Today.AddDays(30)} WHERE id = {plan.Id}",
            TestContext.Current.CancellationToken);

        return (member, plan.Id);
    }

    /// <summary>The member's absence SMS for <see cref="AbsentSince"/>, sent at <paramref name="at"/> unless it failed.</summary>
    private Task AddAbsenceSmsAsync(
        Guid memberId, DateTimeOffset at, NotificationStatus status = NotificationStatus.Sent, SmsDelivery? delivery = null)
    {
        var sms = Notification.ForAbsence(memberId, AbsentSince, Phone(), AbsenceText);
        if (status == NotificationStatus.Failed)
        {
            sms.MarkFailed(errorCode: 418, at).IsSuccess.ShouldBeTrue();
        }
        else
        {
            sms.MarkSent(Interlocked.Increment(ref _phoneSuffix), 1_350m, at).IsSuccess.ShouldBeTrue();
            if (delivery is { } reported)
            {
                sms.RecordDelivery(reported).IsSuccess.ShouldBeTrue();
            }
        }

        return SaveAsync(sms);
    }

    /// <summary>A welcome back the member already had, for an earlier absence.</summary>
    private Task AddWelcomeBackAsync(Guid memberId, DateOnly absentSince)
    {
        var sms = Notification.ForWelcomeBack(memberId, absentSince, Phone(), "متن");
        sms.MarkSent(Interlocked.Increment(ref _phoneSuffix), 1_350m, At(absentSince.AddDays(10), 18, 0)).IsSuccess.ShouldBeTrue();

        return SaveAsync(sms);
    }

    private async Task SaveAsync(Notification notification)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A visit at a chosen moment: still inside on a reserve place, closed half an hour later, or cancelled then.</summary>
    private async Task<Guid> AddVisitAsync(
        (Member Member, Guid PlanId) holder, DateTimeOffset checkedInAt, bool open = false, bool cancelled = false, bool cardioOnly = false)
    {
        var id = Guid.CreateVersion7();
        DateTimeOffset? checkedOutAt = open ? null : checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        // A visit still open holds a place: the first reserve place, since each test has one open visit.
        int? reserveSlot = open ? 1 : null;

        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, is_cardio_only, reserve_slot, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({id}, {holder.Member.Id}, {holder.PlanId}, {cardioOnly}, {reserveSlot}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> WelcomeBacksAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .Where(notification => notification.Kind == NotificationKind.WelcomeBack && notification.MemberId == memberId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Domain.Attendances.Attendance> LatestVisitAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .Where(attendance => attendance.MemberId == memberId)
            .OrderByDescending(attendance => attendance.CheckedInAt)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await using (var scope = Fixture.CreateScope())
        {
            var exists = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                .AnyAsync(user => user.UserName == "owner", TestContext.Current.CancellationToken);
            if (!exists)
            {
                await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
            }
        }

        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}

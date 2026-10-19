using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendDailySms;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The absence SMS (BUSINESS_RULES.md §10 <i>The absence SMS</i>, task 10.12). Like the board's
/// tests (<c>OperationalReportsTests</c>), the plans and visits are written straight into the
/// tables with the dates each case needs, and the run is built as the job builds it, on a clock
/// pinned to 11:00 in Tehran on the day it runs.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AbsenceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int DaysAway = 14;

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly DateOnly Today = new(2026, 9, 30);

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Who gets it ----

    [Fact]
    public async Task Handle_AwayTheDays_SendsItOnceForThatAbsence()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var plan = await AddPlanStartingAsync(member.Id, Today.AddDays(-20));
        await AddVisitAsync(member.Id, plan, At(Today.AddDays(-DaysAway), 18));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);
        await RunAsync(sender);

        var request = sender.Requests.ShouldHaveSingleItem();
        request.Receptor.ShouldBe(member.PhoneNumber);
        request.Text.ShouldBe(SmsText.ForAbsence(member.FullName));
        var absence = (await NotificationsAsync()).ShouldHaveSingleItem();
        absence.Kind.ShouldBe(NotificationKind.Absence);
        absence.MemberId.ShouldBe(member.Id);
        absence.AbsentSince.ShouldBe(Today.AddDays(-DaysAway));
        absence.Status.ShouldBe(NotificationStatus.Sent);
    }

    [Fact]
    public async Task Handle_ADayShortOfTheDays_IsNotSent()
    {
        var member = await AddMemberAsync("علی رضایی");
        var plan = await AddPlanStartingAsync(member.Id, Today.AddDays(-20));
        await AddVisitAsync(member.Id, plan, At(Today.AddDays(-(DaysAway - 1)), 18));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_NeverCameOnThePlan_CountsFromItsStart()
    {
        var due = await AddMemberAsync("رضا نیامده");
        var start = Today.AddDays(-DaysAway);
        await AddPlanStartingAsync(due.Id, start);
        var notYet = await AddMemberAsync("مریم تازه");
        var newPlan = await AddPlanStartingAsync(notYet.Id, Today.AddDays(-(DaysAway - 1)));

        // A visit on an earlier plan does not count against the new one.
        await AddVisitAsync(notYet.Id, newPlan, At(Today.AddDays(-40), 18));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(due.PhoneNumber);
        (await NotificationsAsync()).ShouldHaveSingleItem().AbsentSince.ShouldBe(start);
    }

    [Fact]
    public async Task Handle_CancelledCheckInIsNoVisit_CardioOnlyIs()
    {
        var cancelled = await AddMemberAsync("حسن ورودلغوشده");
        var cancelledPlan = await AddPlanStartingAsync(cancelled.Id, Today.AddDays(-25));
        await AddVisitAsync(cancelled.Id, cancelledPlan, At(Today.AddDays(-20), 18));
        await AddVisitAsync(cancelled.Id, cancelledPlan, At(Today.AddDays(-2), 18), cancelled: true);
        var cardio = await AddMemberAsync("زهرا هوازی");
        var cardioPlan = await AddPlanStartingAsync(cardio.Id, Today.AddDays(-25));
        await AddVisitAsync(cardio.Id, cardioPlan, At(Today.AddDays(-20), 18));
        await AddVisitAsync(cardio.Id, cardioPlan, At(Today.AddDays(-2), 18), cardioOnly: true);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(cancelled.PhoneNumber);
        (await NotificationsAsync()).ShouldHaveSingleItem().AbsentSince.ShouldBe(Today.AddDays(-20));
    }

    [Fact]
    public async Task Handle_NoPlanUsableToday_IsNotSent()
    {
        var deactivated = await AddMemberAsync("غیرفعال", active: false);
        await AddPlanStartingAsync(deactivated.Id, Today.AddDays(-20));
        var frozen = await AddMemberAsync("فریز");
        await AddPlanStartingAsync(frozen.Id, Today.AddDays(-20), frozenSince: Today.AddDays(-15));
        var queued = await AddMemberAsync("پلن آینده");
        await AddPlanStartingAsync(queued.Id, Today.AddDays(1));
        var ended = await AddMemberAsync("پلن تمام‌شده");
        await AddPlanStartingAsync(ended.Id, Today.AddDays(-45));
        var usedUp = await AddMemberAsync("جلسه تمام‌شده");
        await AddPlanStartingAsync(usedUp.Id, Today.AddDays(-20), used: 10);
        var cancelled = await AddMemberAsync("پلن لغوشده");
        await AddPlanStartingAsync(cancelled.Id, Today.AddDays(-20), cancelled: true);
        var singleVisit = await AddMemberAsync("تک‌جلسه");
        await SaveAsync(Subscription.CreateSingleVisit(singleVisit.Id, 150_000m, Today.AddDays(-20)).Value, used: 1, Today.AddDays(-20), null, cancelled: false);
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CameAgainAndStoppedAgain_GetsOneForTheNewAbsence()
    {
        var member = await AddMemberAsync("مهدی برگشته");
        var start = Today.AddDays(-25);
        var plan = await AddPlanStartingAsync(member.Id, start);
        var sender = new ScriptedSmsSender();
        await TurnOnAsync();

        await RunAsync(sender, Today.AddDays(-11));
        await AddVisitAsync(member.Id, plan, At(Today.AddDays(-10), 18));
        await RunAsync(sender, Today);
        await RunAsync(sender, Today.AddDays(4));

        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync())
            .Select(notification => notification.AbsentSince)
            .OrderBy(day => day)
            .ShouldBe([start, Today.AddDays(-10)]);
    }

    [Fact]
    public async Task Handle_FailedBefore_IsNotWrittenAgain()
    {
        var member = await AddMemberAsync("نرگس ناموفق");
        var plan = await AddPlanStartingAsync(member.Id, Today.AddDays(-20));
        await AddVisitAsync(member.Id, plan, At(Today.AddDays(-DaysAway), 18));
        await TurnOnAsync();

        await RunAsync(new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 411)));
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        // A failed message counts as written; the Owner resends it from «پیامک‌ها».
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Failed);
    }

    // ---- Helpers ----

    /// <summary>All SMS on and only the absence SMS, 14 days at 11:00, as the Owner would on the page.</summary>
    private async Task TurnOnAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            enabled: true,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: new SmsKindSettings(true, DaysAway, new TimeOnly(11, 0)),
            broadcastEnabled: false).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The run as the job makes it, at 11:00 in Tehran on <paramref name="day"/> (<see cref="Today"/> by default).</summary>
    private async Task<SmsRunResult> RunAsync(ScriptedSmsSender sender, DateOnly? day = null)
    {
        var time = new FakeTimeProvider(At(day ?? Today, 11, 0));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendDailySmsHandler(
            db,
            calendar,
            new SmsBatchSender(db, calendar, time, sender, NoWait),
            scope.ServiceProvider.GetRequiredService<DailyReportCandidate>(),
            scope.ServiceProvider.GetRequiredService<DebtReminderCandidate>(),
            new AbsenceCandidate(db, calendar),
            NullLogger<SendDailySmsHandler>.Instance);

        return await handler.Handle(NotificationKind.Absence, TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private async Task<Member> AddMemberAsync(string fullName, bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98917{suffix:D7}");
        if (!active)
        {
            member.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A 10-session, 30-day plan starting on <paramref name="startDate"/>, sold at noon that day.</summary>
    private async Task<Guid> AddPlanStartingAsync(
        Guid memberId, DateOnly startDate, int used = 0, DateOnly? frozenSince = null, bool cancelled = false)
    {
        var plan = Subscription.CreateMembership(memberId, sessionCount: 10, sessionPrice: 90_000m, startDate).Value;

        return await SaveAsync(plan, used, startDate, frozenSince, cancelled);
    }

    private async Task<Guid> SaveAsync(
        Subscription plan, int used, DateOnly soldOn, DateOnly? frozenSince, bool cancelled)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var soldAt = At(soldOn, 12);
        DateTimeOffset? cancelledAt = cancelled ? soldAt : null;
        var reason = cancelled ? "انصراف" : null;
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE subscriptions
            SET used_sessions = {used}, created_at = {soldAt}, frozen_since = {frozenSince},
                cancelled_at = {cancelledAt}, cancellation_reason = {reason}
            WHERE id = {plan.Id}
            """,
            TestContext.Current.CancellationToken);

        return plan.Id;
    }

    /// <summary>A closed visit at a chosen moment; a cancelled one closes when it was cancelled.</summary>
    private async Task AddVisitAsync(
        Guid memberId, Guid subscriptionId, DateTimeOffset checkedInAt, bool cancelled = false, bool cardioOnly = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOutAt = checkedInAt.AddMinutes(30);
        DateTimeOffset? cancelledAt = cancelled ? checkedOutAt : null;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO attendances (id, member_id, subscription_id, is_cardio_only, checked_in_at, checked_out_at, cancelled_at, created_at)
            VALUES ({Guid.CreateVersion7()}, {memberId}, {subscriptionId}, {cardioOnly}, {checkedInAt}, {checkedOutAt}, {cancelledAt}, now())
            """,
            TestContext.Current.CancellationToken);
    }
}

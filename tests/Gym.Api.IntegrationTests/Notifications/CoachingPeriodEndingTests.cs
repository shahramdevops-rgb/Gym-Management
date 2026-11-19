using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendDailySms;
using Gym.Domain.Coaching;
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

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The coaching period's last days (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7n). The
/// links and periods are written straight into the tables with the dates each case needs, and the
/// run is built as the job builds it, on a clock pinned to 11:00 in Tehran on the day it runs, as in
/// <see cref="AbsenceTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CoachingPeriodEndingTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int DaysBefore = 7;

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly DateOnly Today = new(2026, 11, 19);

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Who gets it ----

    [Fact]
    public async Task Handle_SevenDaysLeft_SendsItOnceForThatLastDay()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var trainer = await AddTrainerAsync("علی شریفی‌پور");
        var link = await LinkAsync(member, trainer, DaysLeftOf30(DaysBefore));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);
        await RunAsync(sender);

        var request = sender.Requests.ShouldHaveSingleItem();
        request.Receptor.ShouldBe(member.PhoneNumber);
        request.Text.ShouldBe(SmsText.ForCoachingPeriodEnding(member.FullName, trainer.FullName, DaysBefore));
        var sent = (await NotificationsAsync()).ShouldHaveSingleItem();
        sent.Kind.ShouldBe(NotificationKind.CoachingPeriodEnding);
        sent.MemberId.ShouldBe(member.Id);
        sent.TrainerLinkId.ShouldBe(link.Id);
        sent.PeriodEndsOn.ShouldBe(Today.AddDays(DaysBefore - 1));
        sent.Status.ShouldBe(NotificationStatus.Sent);
    }

    [Fact]
    public async Task Handle_ADayBeforeTheWindow_IsNotSent()
    {
        var member = await AddMemberAsync("علی رضایی");
        await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore + 1));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MissedRuns_SendsTheRealDaysLeftLater()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var trainer = await AddTrainerAsync("مهدی نوری");
        await LinkAsync(member, trainer, DaysLeftOf30(DaysBefore));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        // The runs of the first two days did not happen (the server was down).
        await RunAsync(sender, Today.AddDays(2));

        sender.Requests.ShouldHaveSingleItem().Text
            .ShouldBe(SmsText.ForCoachingPeriodEnding(member.FullName, trainer.FullName, DaysBefore - 2));
    }

    [Fact]
    public async Task Handle_LastDay_StillSendsOneDayLeft()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var trainer = await AddTrainerAsync("مهدی نوری");
        await LinkAsync(member, trainer, DaysLeftOf30(1));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Text.ShouldBe(SmsText.ForCoachingPeriodEnding(member.FullName, trainer.FullName, 1));
    }

    [Fact]
    public async Task Handle_TheSettingsNumber_DecidesTheWindow()
    {
        var member = await AddMemberAsync("سارا محمدی");
        await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(12));
        await TurnOnAsync(daysBefore: 12);
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Text.ShouldContain("۱۲ روز تا پایان");
    }

    // ---- Once per last day ----

    [Fact]
    public async Task Handle_RenewedAfterItsSms_TheNewLastDayGetsItsOwn()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var link = await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        // «تمدید» of 30 days right after the last day.
        var firstLastDay = Today.AddDays(DaysBefore - 1);
        await InsertPeriodAsync(link.Id, firstLastDay.AddDays(1), firstLastDay.AddDays(30));
        await RunAsync(sender, Today.AddDays(1));
        await RunAsync(sender, firstLastDay.AddDays(30 - DaysBefore + 1));

        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync())
            .Select(notification => notification.PeriodEndsOn)
            .OrderBy(day => day)
            .ShouldBe([firstLastDay, firstLastDay.AddDays(30)]);
    }

    [Fact]
    public async Task Handle_LastDayEditedAfterItsSms_TheNewLastDayGetsItsOwnAtOnce()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var trainer = await AddTrainerAsync("مهدی نوری");
        var link = await LinkAsync(member, trainer, DaysLeftOf30(DaysBefore));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();
        await RunAsync(sender);

        // «ویرایش دوره» the next day: 3 days left from then.
        await SetLastDayAsync(link.Id, Today.AddDays(3));
        await RunAsync(sender, Today.AddDays(1));

        sender.Requests.Count.ShouldBe(2);
        sender.Requests[1].Text.ShouldBe(SmsText.ForCoachingPeriodEnding(member.FullName, trainer.FullName, 3));
    }

    [Fact]
    public async Task Handle_FailedBefore_IsNotWrittenAgain()
    {
        var member = await AddMemberAsync("نرگس ناموفق");
        await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore));
        await TurnOnAsync();

        await RunAsync(new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 411)));
        var sender = new ScriptedSmsSender();
        await RunAsync(sender, Today.AddDays(1));

        // A failed message counts as written; the Owner resends it from «پیامک‌ها».
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Failed);
    }

    // ---- Who does not ----

    [Fact]
    public async Task Handle_NoneForEndedDeactivatedUnsetOverOrShort_OnlyTheOneDue()
    {
        var trainer = await AddTrainerAsync("مهدی نوری");
        var due = await AddMemberAsync("سارا محمدی");
        await LinkAsync(due, trainer, DaysLeftOf30(DaysBefore));

        // The coaching ended: the student is no longer the trainer's.
        await LinkAsync(await AddMemberAsync("حسن تمام‌شده"), trainer, DaysLeftOf30(DaysBefore), ended: true);

        // The desk deactivated the member.
        await LinkAsync(await AddMemberAsync("رضا غیرفعال", active: false), trainer, DaysLeftOf30(DaysBefore));

        // A link from before the periods: no last day to speak of.
        var unset = await LinkAsync(await AddMemberAsync("مریم قدیمی"), trainer, DaysLeftOf30(DaysBefore));
        await RemovePeriodsAsync(unset.Id);

        // Over yesterday: «دوره تمام شد», nothing left to tell.
        await LinkAsync(await AddMemberAsync("مهدی تمام‌دوره"), trainer, DaysLeftOf30(0));

        // A last period of 7 days: it would come on its first day.
        await LinkAsync(await AddMemberAsync("نرگس کوتاه"), trainer, (Today, DaysBefore));

        // Renewed past the window: the last day is the renewal's.
        var renewed = await LinkAsync(await AddMemberAsync("علی تمدیدی"), trainer, DaysLeftOf30(DaysBefore));
        await InsertPeriodAsync(renewed.Id, Today.AddDays(DaysBefore), Today.AddDays(DaysBefore + 29));
        await TurnOnAsync();
        var sender = new ScriptedSmsSender();

        await RunAsync(sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(due.PhoneNumber);
    }

    [Fact]
    public async Task Handle_KindOff_SendsNothing()
    {
        await LinkAsync(await AddMemberAsync("سارا محمدی"), await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore));
        await TurnOnAsync(enabled: false);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(sender);

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    // ---- The database ----

    [Fact]
    public async Task Database_ASecondForTheSameLinkAndLastDay_IsRefused()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var link = await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore));
        var lastDay = Today.AddDays(DaysBefore - 1);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForCoachingPeriodEnding(member.Id, link.Id, lastDay, member.PhoneNumber, "اول"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Notifications.Add(Notification.ForCoachingPeriodEnding(member.Id, link.Id, lastDay, member.PhoneNumber, "دوم"));
        var refused = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgres = refused.InnerException.ShouldBeOfType<PostgresException>();
        postgres.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.ShouldBe(NotificationConstraints.OneCoachingPeriodEndingPerLinkAndLastDay);
    }

    [Fact]
    public async Task Database_AnotherKindNamingALink_IsRefused()
    {
        var member = await AddMemberAsync("سارا محمدی");
        var link = await LinkAsync(member, await AddTrainerAsync("مهدی نوری"), DaysLeftOf30(DaysBefore));
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var absence = Notification.ForAbsence(member.Id, Today.AddDays(-20), member.PhoneNumber, "غیبت");
        db.Notifications.Add(absence);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE notifications SET trainer_link_id = {link.Id}, period_ends_on = {Today} WHERE id = {absence.Id}",
            TestContext.Current.CancellationToken));

        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe(NotificationConstraints.LinkOnlyForCoachingPeriodEnding);
    }

    [Fact]
    public async Task Database_DaysOutOfRange_IsRefused()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE sms_settings SET coaching_period_ending_days = 15",
            TestContext.Current.CancellationToken));

        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe("ck_sms_settings_coaching_period_ending_days");
    }

    [Fact]
    public async Task Database_OnWithNoTime_IsRefused()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE sms_settings SET coaching_period_ending_enabled = true, coaching_period_ending_send_time = NULL",
            TestContext.Current.CancellationToken));

        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe("ck_sms_settings_coaching_period_ending_on_means_filled");
    }

    // ---- Helpers ----

    /// <summary>A 30-day period whose last day, counted as one, leaves <paramref name="daysLeft"/> days today.</summary>
    private static (DateOnly StartsOn, int Days) DaysLeftOf30(int daysLeft) => (Today.AddDays(daysLeft - 30), 30);

    /// <summary>All SMS on and only this kind, at 11:00, as the Owner would on the page.</summary>
    private async Task TurnOnAsync(int daysBefore = DaysBefore, bool enabled = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            enabled: true,
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
            sessionCancelledEnabled: false,
            coachingPeriodEnding: new SmsKindSettings(enabled, daysBefore, new TimeOnly(11, 0))).IsSuccess.ShouldBeTrue();
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
            new CoachingPeriodEndingCandidate(db),
            NullLogger<SendDailySmsHandler>.Instance);

        return await handler.Handle(NotificationKind.CoachingPeriodEnding, TestContext.Current.CancellationToken);
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
        var member = TestMembers.Seed(fullName, $"+98918{suffix:D7}");
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

    private async Task<Trainer> AddTrainerAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var trainer = Trainer.Create(fullName, $"+98919{suffix:D7}", new DateOnly(2026, 10, 1)).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return trainer;
    }

    /// <summary>The trainer accepted the member's request on the period's first day, with its days.</summary>
    private async Task<TrainerLink> LinkAsync(Member member, Trainer trainer, (DateOnly StartsOn, int Days) period, bool ended = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = TrainerRequest.Create(member.Id, trainer.Id, period.StartsOn, madeToday: 0).Value;
        db.TrainerRequests.Add(request);
        var link = request.Accept(period.StartsOn, period.Days).Value;
        if (ended)
        {
            link.End(period.StartsOn.AddDays(1), TrainerLinkEndReason.ByMember).IsSuccess.ShouldBeTrue();
        }

        db.TrainerLinks.Add(link);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return link;
    }

    private async Task InsertPeriodAsync(Guid linkId, DateOnly startsOn, DateOnly endsOn)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"INSERT INTO coaching_periods (id, link_id, starts_on, ends_on, created_at) VALUES ({Guid.CreateVersion7()}, {linkId}, {startsOn}, {endsOn}, now())",
            TestContext.Current.CancellationToken);
    }

    /// <summary>«ویرایش دوره» as it ends: the link's one period now ends on <paramref name="lastDay"/>.</summary>
    private async Task SetLastDayAsync(Guid linkId, DateOnly lastDay)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE coaching_periods SET ends_on = {lastDay} WHERE link_id = {linkId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>Makes the link one from before 15.7h: no period row.</summary>
    private async Task RemovePeriodsAsync(Guid linkId)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"DELETE FROM coaching_periods WHERE link_id = {linkId}",
            TestContext.Current.CancellationToken);
    }
}

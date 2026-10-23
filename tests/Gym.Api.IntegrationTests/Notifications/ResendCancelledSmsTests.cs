using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.CheckSmsDelivery;
using Gym.Application.Notifications.ResendCancelledSms;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Jobs;
using Gym.Infrastructure.Persistence;

using Hangfire;
using Hangfire.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The automatic resend of what the provider cancelled (BUSINESS_RULES.md §10 <i>Sending</i>, task
/// 10.16): today's messages only, 30 minutes after the last request, at most five times, within the
/// sending hours and with the switches on. Built the way Hangfire builds
/// <see cref="ResendCancelledSmsHandler"/>, on a clock pinned to Tehran's 19:00 on <see cref="Today"/>,
/// with a provider that answers what each test scripts.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ResendCancelledSmsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Name = "سارا محمدی";
    private const string OwnerPhone = "+989351112233";
    private const string Text = "سارا محمدی عزیز، ۱۴۰۵/۰۷/۲۰ روز تولد شماست؛ آن روز مهمان باشگاه پاسارگاد هستید.";

    /// <summary>1405/07/16.</summary>
    private static readonly DateOnly Today = new(2026, 10, 8);

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static int _phoneSuffix;
    private static int _providerId;

    // ---- Sent again ----

    [Fact]
    public async Task Handle_CancelledTodayAnHourAgo_SendsExactlyTheSameMessageAgain()
    {
        await TurnOnBirthdayAsync();
        var (id, phone) = await AddCancelledAsync(At(Today, 18, 0));
        var sender = new ScriptedSmsSender();

        var resent = await HandleAsync(sender);

        resent.ShouldBe(1);
        sender.Requests.ShouldHaveSingleItem().ShouldBe(new SmsMessage(phone, Text));
        var stored = await StoredAsync(id);
        stored.Status.ShouldBe(NotificationStatus.Sent);
        stored.Delivery.ShouldBeNull();
        stored.AutoResends.ShouldBe(1);
        stored.Attempts.ShouldBe(2);
        stored.SentAt.ShouldBe(At(Today, 19, 0));
        stored.CostRial.ShouldBe(ScriptedSmsSender.Cost);
    }

    [Fact]
    public async Task Handle_ProviderCancelledItSinceTheLastCheck_FindsItAndSendsItAgain()
    {
        // The check every 15 minutes is what finds a cancelled message the same evening.
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 18, 0), cancelled: false);
        var account = new ScriptedAccount { [await ProviderIdOfAsync(id)] = SmsDelivery.Cancelled };
        var sender = new ScriptedSmsSender();

        var resent = await HandleAsync(sender, account: account);

        resent.ShouldBe(1);
        (await StoredAsync(id)).AutoResends.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ExactlyThirtyMinutesAfter_SendsItAgain()
    {
        await TurnOnBirthdayAsync();
        await AddCancelledAsync(At(Today, 18, 30));
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_DailyReportCancelledAt2245_IsSentAgainBeforeMidnight()
    {
        // The daily report's hours run until midnight (§10).
        await SaveSettingsAsync(settings => settings.Update(
            enabled: true, SmsKindSettings.Off, LowSessionsSettings.Off, SmsKindSettings.Off, SmsKindSettings.Off,
            ownerPhone: null, dailyReportEnabled: true, OwnerPhone, dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false, debtReminder: SmsKindSettings.Off, debtReminderMinAmount: null,
            absence: SmsKindSettings.Off, broadcastEnabled: false));
        var report = Notification.ForDailyReport(Today, OwnerPhone, "گزارش باشگاه پاسارگاد ۰۷/۱۶\nورود: ۸۵");
        Cancel(report, At(Today, 22, 45));
        await SaveAsync(report);
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender, At(Today, 23, 30));

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(OwnerPhone);
    }

    [Fact]
    public async Task Handle_FourthTimeCancelled_IsSentAgainAsTheFifth()
    {
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 18, 0), earlierAutoResends: Notification.MaxAutoResends - 1);
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.Count.ShouldBe(1);
        (await StoredAsync(id)).AutoResends.ShouldBe(Notification.MaxAutoResends);
    }

    // ---- Left as it is ----

    [Fact]
    public async Task Handle_CancelledLessThanThirtyMinutesAgo_WaitsForTheNextTime()
    {
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 18, 31));
        var sender = new ScriptedSmsSender();

        var resent = await HandleAsync(sender);

        resent.ShouldBe(0);
        sender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Delivery.ShouldBe(SmsDelivery.Cancelled);
    }

    [Fact]
    public async Task Handle_CancelledYesterday_IsLeftForTheOwner()
    {
        // §10: the same day only; what is still cancelled at the end of the hours stays so.
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today.AddDays(-1), 21, 0));
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender, At(Today, 10, 0));

        sender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Delivery.ShouldBe(SmsDelivery.Cancelled);
    }

    [Theory]
    [InlineData(22, 0, 1)]
    [InlineData(23, 30, 0)]
    public async Task Handle_AfterTheSendingHours_SendsNothing(int hour, int minute, int second)
    {
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 21, 0));
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender, At(Today, hour, minute, second));

        sender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Delivery.ShouldBe(SmsDelivery.Cancelled);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Handle_AllSmsOrItsKindOff_SendsNothing(bool allSms, bool birthday)
    {
        // §10: the switches stop every automatic send; only the Owner's resend is allowed then.
        await TurnOnBirthdayAsync(allSms, birthday);
        var (id, _) = await AddCancelledAsync(At(Today, 18, 0));
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Delivery.ShouldBe(SmsDelivery.Cancelled);
    }

    [Fact]
    public async Task Handle_FiveAutomaticResendsUsed_IsLeftForTheOwner()
    {
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 18, 0), earlierAutoResends: Notification.MaxAutoResends);
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.ShouldBeEmpty();
        var stored = await StoredAsync(id);
        stored.Delivery.ShouldBe(SmsDelivery.Cancelled);
        stored.AutoResends.ShouldBe(Notification.MaxAutoResends);
    }

    [Theory]
    [InlineData(SmsDelivery.Delivered)]
    [InlineData(SmsDelivery.NotDelivered)]
    [InlineData(SmsDelivery.BlockedByReceiver)]
    public async Task Handle_NotCancelled_SendsNothing(SmsDelivery delivery)
    {
        // Not delivered or blocked was paid for, or would be blocked again: never resent by itself.
        await TurnOnBirthdayAsync();
        var member = await AddMemberAsync();
        var notification = Notification.ForBirthday(member.Id, 1405, member.PhoneNumber, Text);
        notification.MarkSent(NextProviderId(), 1_350m, At(Today, 18, 0));
        notification.RecordDelivery(delivery);
        await SaveAsync(notification);
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_FailedOrUnknown_IsLeftForTheOwner()
    {
        // A failure may have cost money and an unknown one may have arrived: the Owner decides.
        await TurnOnBirthdayAsync();
        var failed = Notification.ForBirthday((await AddMemberAsync()).Id, 1405, "+989121111111", Text);
        failed.MarkFailed(424, At(Today, 18, 0));
        var unknown = Notification.ForBirthday((await AddMemberAsync()).Id, 1405, "+989121111112", Text);
        unknown.MarkUnknown(At(Today, 18, 0));
        await SaveAsync(failed);
        await SaveAsync(unknown);
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);

        sender.Requests.ShouldBeEmpty();
    }

    // ---- How it went ----

    [Fact]
    public async Task Handle_CreditUsedUp_IsFailedAndTheRestStayCancelled()
    {
        await TurnOnBirthdayAsync();
        var (first, _) = await AddCancelledAsync(At(Today, 18, 0));
        var (second, _) = await AddCancelledAsync(At(Today, 18, 10));
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: SmsProviderCodes.CreditUsedUp));

        var resent = await HandleAsync(sender);

        resent.ShouldBe(0);
        sender.Requests.Count.ShouldBe(1);
        var stored = await StoredAsync(first);
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.ErrorCode.ShouldBe(SmsProviderCodes.CreditUsedUp);
        (await StoredAsync(second)).Delivery.ShouldBe(SmsDelivery.Cancelled);
    }

    [Fact]
    public async Task Handle_FailureThatMayPass_IsFailedForTheOwnerToResend()
    {
        await TurnOnBirthdayAsync();
        var (id, _) = await AddCancelledAsync(At(Today, 18, 0));
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        await HandleAsync(sender);

        var stored = await StoredAsync(id);
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.ErrorCode.ShouldBe(409);
        stored.AutoResends.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_TwiceInARow_SendsItOnce()
    {
        // The second finds it sent and not cancelled (yet): nothing to do.
        await TurnOnBirthdayAsync();
        await AddCancelledAsync(At(Today, 18, 0));
        var sender = new ScriptedSmsSender();

        await HandleAsync(sender);
        await HandleAsync(sender);

        sender.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public void Schedule_AtStartup_RunsEvery15MinutesInTehran()
    {
        using var connection = Fixture.Services.GetRequiredService<JobStorage>().GetConnection();

        var job = connection.GetRecurringJobs().Single(job => job.Id == RecurringJobScheduler.SmsCancelledResendJobId);

        job.Cron.ShouldBe("*/15 * * * *");
        job.TimeZoneId.ShouldBe(Fixture.Services.GetRequiredService<IGymCalendar>().TimeZone.Id);
        job.Job.Type.ShouldBe(typeof(CancelledSmsResendJob));
        ResendCancelledSmsHandler.WaitBetweenTries.ShouldBe(TimeSpan.FromMinutes(30));
    }

    // ---- Helpers ----

    private async Task<int> HandleAsync(ISmsSender sender, DateTimeOffset? at = null, ScriptedAccount? account = null)
    {
        var time = new FakeTimeProvider(at ?? At(Today, 19, 0));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var deliveries = new CheckSmsDeliveryHandler(
            db, time, account ?? new ScriptedAccount(), NullLogger<CheckSmsDeliveryHandler>.Instance);
        var handler = new ResendCancelledSmsHandler(
            db, calendar, time, sender, deliveries, NullLogger<ResendCancelledSmsHandler>.Instance);

        return await handler.Handle(TestContext.Current.CancellationToken);
    }

    private Task TurnOnBirthdayAsync(bool allSms = true, bool birthday = true) =>
        SaveSettingsAsync(settings => settings.Update(
            allSms,
            SmsKindSettings.Off,
            LowSessionsSettings.Off,
            new SmsKindSettings(birthday, 2, new TimeOnly(18, 0)),
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false));

    private async Task SaveSettingsAsync(Func<SmsSettings, Result> update)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        update(settings).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A birthday SMS for a new member, sent at <paramref name="sentAt"/> and cancelled by the provider,
    /// after <paramref name="earlierAutoResends"/> automatic resends that were cancelled too.
    /// </summary>
    /// <param name="cancelled"><c>false</c>: sent, and the provider has not answered yet.</param>
    private async Task<(Guid Id, string Phone)> AddCancelledAsync(
        DateTimeOffset sentAt, int earlierAutoResends = 0, bool cancelled = true)
    {
        var member = await AddMemberAsync();
        var notification = Notification.ForBirthday(member.Id, 1405, member.PhoneNumber, Text);
        for (var resend = 0; resend < earlierAutoResends; resend++)
        {
            Cancel(notification, sentAt.AddHours(-3));
            notification.PrepareAutoResend().IsSuccess.ShouldBeTrue();
        }

        if (cancelled)
        {
            Cancel(notification, sentAt);
        }
        else
        {
            notification.MarkSent(NextProviderId(), 1_350m, sentAt).IsSuccess.ShouldBeTrue();
        }

        await SaveAsync(notification);

        return (notification.Id, member.PhoneNumber);
    }

    private static void Cancel(Notification notification, DateTimeOffset sentAt)
    {
        notification.MarkSent(NextProviderId(), 1_350m, sentAt).IsSuccess.ShouldBeTrue();
        notification.RecordDelivery(SmsDelivery.Cancelled).IsSuccess.ShouldBeTrue();
    }

    private static long NextProviderId() => Interlocked.Increment(ref _providerId) + 2_000_000L;

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(Name, $"+98915{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task SaveAsync(Notification notification)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Notification> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<long> ProviderIdOfAsync(Guid id) => (await StoredAsync(id)).ProviderMessageId!.Value;

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute, int second = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute, second), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    /// <summary>Answers with the deliveries it was given; nothing else is known.</summary>
    private sealed class ScriptedAccount : Dictionary<long, SmsDelivery>, ISmsAccount
    {
        public Task<IReadOnlyDictionary<long, SmsDelivery>> GetDeliveriesAsync(
            IReadOnlyCollection<long> providerMessageIds, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<long, SmsDelivery> answer = this
                .Where(pair => providerMessageIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            return Task.FromResult(answer);
        }

        public Task<SmsCredit> GetCreditAsync(CancellationToken cancellationToken) => Task.FromResult(SmsCredit.TestMode);
    }
}

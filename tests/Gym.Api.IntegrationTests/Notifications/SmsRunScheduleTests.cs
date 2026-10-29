using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Jobs;
using Gym.Infrastructure.Persistence;

using Hangfire;
using Hangfire.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The real schedule: each kind that is on becomes a Hangfire recurring job at its send time, in the
/// gym's zone, and a kind that is off has none (BUSINESS_RULES.md §10 <i>The daily runs</i>).
/// </summary>
/// <remarks>
/// It writes to the test database's own Hangfire tables, and every test removes what it wrote, so the
/// test host's Hangfire server never finds a run to fire.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsRunScheduleTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Apply_KindOn_SchedulesItsRunAtItsTimeInTehran()
    {
        var settings = await SettingsAsync(enabled: true, expiringOn: true, sendTime: new TimeOnly(10, 15));

        try
        {
            Schedule().Apply(settings);

            var job = RecurringJobs().ShouldHaveSingleItem();
            job.Id.ShouldBe("sms-SubscriptionExpiring");
            job.Cron.ShouldBe("15 10 * * *");
            job.TimeZoneId.ShouldBe(Calendar().TimeZone.Id);
            job.Job.Type.ShouldBe(typeof(DailySmsJob));
            job.Job.Args[0].ShouldBe(NotificationKind.SubscriptionExpiring);
        }
        finally
        {
            RemoveAll();
        }
    }

    [Fact]
    public async Task Apply_DailyReportOn_SchedulesItsRunAt2245InTehran()
    {
        var settings = await SettingsAsync(enabled: true, expiringOn: false, sendTime: new TimeOnly(10, 0), dailyReportOn: true);

        try
        {
            Schedule().Apply(settings);

            var job = RecurringJobs().ShouldHaveSingleItem();
            job.Id.ShouldBe("sms-DailyReport");
            job.Cron.ShouldBe("45 22 * * *");
            job.TimeZoneId.ShouldBe(Calendar().TimeZone.Id);
            job.Job.Args[0].ShouldBe(NotificationKind.DailyReport);
        }
        finally
        {
            RemoveAll();
        }
    }

    [Fact]
    public async Task Apply_SubscriptionReceiptOn_SchedulesNoDailyRun()
    {
        // The receipt goes at each payment (task 10.9), so it has no recurring job.
        var settings = await SettingsAsync(enabled: true, expiringOn: false, sendTime: new TimeOnly(10, 0), receiptOn: true);

        try
        {
            Schedule().Apply(settings);

            RecurringJobs().ShouldBeEmpty();
        }
        finally
        {
            RemoveAll();
        }
    }

    [Fact]
    public void ReceiptSendAt_SchedulesTheReceiptJobForThatMoment()
    {
        // Far ahead, so the test host's Hangfire server never runs it before it is deleted.
        var at = new DateTimeOffset(2099, 1, 1, 6, 30, 0, TimeSpan.Zero);
        var subscriptionId = Guid.CreateVersion7();
        var jobs = Fixture.Services.GetRequiredService<IBackgroundJobClient>();

        new HangfireSubscriptionReceiptScheduler(jobs).SendAt(subscriptionId, at);

        var monitoring = Fixture.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        var (id, job) = monitoring.ScheduledJobs(0, 1000)
            .Single(scheduled => scheduled.Value.Job.Args[0] is Guid argument && argument == subscriptionId);
        try
        {
            job.Job.Type.ShouldBe(typeof(SubscriptionReceiptJob));
            job.Job.Method.Name.ShouldBe(nameof(SubscriptionReceiptJob.Send));
            job.EnqueueAt.ShouldBe(at.UtcDateTime);
        }
        finally
        {
            jobs.Delete(id);
        }
    }

    [Fact]
    public async Task Apply_TimeChanged_MovesTheRun()
    {
        try
        {
            Schedule().Apply(await SettingsAsync(enabled: true, expiringOn: true, sendTime: new TimeOnly(10, 15)));

            Schedule().Apply(await SettingsAsync(enabled: true, expiringOn: true, sendTime: new TimeOnly(18, 30)));

            RecurringJobs().ShouldHaveSingleItem().Cron.ShouldBe("30 18 * * *");
        }
        finally
        {
            RemoveAll();
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Apply_KindOrAllSmsTurnedOff_RemovesTheRun(bool enabled, bool expiringOn)
    {
        try
        {
            Schedule().Apply(await SettingsAsync(enabled: true, expiringOn: true, sendTime: new TimeOnly(10, 15)));

            Schedule().Apply(await SettingsAsync(enabled, expiringOn, sendTime: new TimeOnly(10, 15)));

            RecurringJobs().ShouldBeEmpty();
        }
        finally
        {
            RemoveAll();
        }
    }

    // ---- Helpers ----

    private HangfireSmsRunSchedule Schedule() =>
        new(Fixture.Services.GetRequiredService<IRecurringJobManager>(), Calendar());

    private IGymCalendar Calendar() => Fixture.Services.GetRequiredService<IGymCalendar>();

    /// <summary>The seeded row, changed in memory only: the schedule reads nothing else.</summary>
    private async Task<SmsSettings> SettingsAsync(
        bool enabled, bool expiringOn, TimeOnly sendTime, bool dailyReportOn = false, bool receiptOn = false)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

        settings.Update(
            enabled,
            new SmsKindSettings(expiringOn, 3, sendTime),
            LowSessionsSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: dailyReportOn,
            dailyReportOn ? "+989351112233" : null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: receiptOn,
            welcomeEnabled: false,
            welcomeBackEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false, trainerRequestEnabled: false).IsSuccess.ShouldBeTrue();

        return settings;
    }

    private List<RecurringJobDto> RecurringJobs()
    {
        using var connection = Fixture.Services.GetRequiredService<JobStorage>().GetConnection();

        return connection.GetRecurringJobs().Where(job => job.Id.StartsWith("sms-", StringComparison.Ordinal)).ToList();
    }

    private void RemoveAll()
    {
        var recurringJobs = Fixture.Services.GetRequiredService<IRecurringJobManager>();
        foreach (var kind in Enum.GetValues<NotificationKind>())
        {
            recurringJobs.RemoveIfExists(HangfireSmsRunSchedule.JobIdFor(kind));
        }
    }
}

using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each kind's daily SMS run as a Hangfire recurring job (BUSINESS_RULES.md §10 <i>The daily runs</i>),
/// at the kind's send time in <c>Gym:TimeZone</c>.
/// </summary>
/// <remarks>
/// Hangfire keeps recurring jobs in its own tables, so the runs outlive a restart and nothing has to
/// schedule them at startup; a backup carries them with the settings they were made from.
/// <c>AddOrUpdate</c> on the same id moves the run, and <c>RemoveIfExists</c> takes it away.
/// </remarks>
public sealed class HangfireSmsRunSchedule(IRecurringJobManager recurringJobs, IGymCalendar calendar) : ISmsRunSchedule
{
    public static string JobIdFor(NotificationKind kind) => $"sms-{kind}";

    public void Apply(SmsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var kind in Enum.GetValues<NotificationKind>())
        {
            var kindSettings = settings.For(kind);
            var jobId = JobIdFor(kind);

            if (settings.Enabled && kindSettings.Enabled && kindSettings.SendTime is { } sendTime)
            {
                // A cron "minute hour * * *", in the gym's zone so it fires at that local time whatever
                // the server's own clock says (the same as the auto-checkout job).
                recurringJobs.AddOrUpdate<DailySmsJob>(
                    jobId,
                    job => job.Run(kind, CancellationToken.None),
                    $"{sendTime.Minute} {sendTime.Hour} * * *",
                    new RecurringJobOptions { TimeZone = calendar.TimeZone });
            }
            else
            {
                recurringJobs.RemoveIfExists(jobId);
            }
        }
    }
}

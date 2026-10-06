using Gym.Domain.Notifications;

namespace Gym.Application.Common.Sms;

/// <summary>
/// Puts each kind's daily SMS run at its send time (BUSINESS_RULES.md §10 <i>The daily runs</i>):
/// a kind that is on runs every day at its time, in the gym's time zone; a kind that is off, or
/// every kind while all SMS are off, has no run.
/// </summary>
/// <remarks>
/// Called after the settings page is saved, so a change of time applies from the next run. The job
/// runner (Hangfire, in Gym.Infrastructure/Jobs) keeps the schedule in the database, so nothing has
/// to put it back when the app restarts.
/// </remarks>
public interface ISmsRunSchedule
{
    void Apply(SmsSettings settings);
}

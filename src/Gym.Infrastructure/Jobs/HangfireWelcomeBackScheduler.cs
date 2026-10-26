using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each welcome back as a Hangfire background job (BUSINESS_RULES.md §10 <i>The welcome back</i>):
/// scheduled for half an hour after the check-in, kept in Hangfire's own tables like the welcomes.
/// </summary>
public sealed class HangfireWelcomeBackScheduler(IBackgroundJobClient jobs) : IWelcomeBackScheduler
{
    public void SendAt(Guid attendanceId, DateTimeOffset at) =>
        jobs.Schedule<WelcomeBackJob>(job => job.Send(attendanceId, CancellationToken.None), at);
}

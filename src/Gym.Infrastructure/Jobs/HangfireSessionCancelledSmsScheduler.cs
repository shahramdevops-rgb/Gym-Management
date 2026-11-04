using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each student's SMS about a cancelled session as a Hangfire background job (BUSINESS_RULES.md §15
/// <i>Timetable and attendance</i>, task 15.6c): enqueued for now, or scheduled for 08:00.
/// </summary>
public sealed class HangfireSessionCancelledSmsScheduler(IBackgroundJobClient jobs) : ISessionCancelledSmsScheduler
{
    public void SendNow(Guid changeId, Guid memberId) =>
        jobs.Enqueue<SessionCancelledSmsJob>(job => job.Send(changeId, memberId, CancellationToken.None));

    public void SendAt(Guid changeId, Guid memberId, DateTimeOffset at) =>
        jobs.Schedule<SessionCancelledSmsJob>(job => job.Send(changeId, memberId, CancellationToken.None), at);
}

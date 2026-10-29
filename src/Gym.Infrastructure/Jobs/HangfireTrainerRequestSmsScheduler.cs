using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each trainer's SMS about a request as a Hangfire background job (BUSINESS_RULES.md §15 <i>A member
/// and their trainer</i>, task 15.4c): enqueued for now, or scheduled for 10:00, like the welcome.
/// </summary>
public sealed class HangfireTrainerRequestSmsScheduler(IBackgroundJobClient jobs) : ITrainerRequestSmsScheduler
{
    public void SendNow(Guid requestId) =>
        jobs.Enqueue<TrainerRequestSmsJob>(job => job.Send(requestId, CancellationToken.None));

    public void SendAt(Guid requestId, DateTimeOffset at) =>
        jobs.Schedule<TrainerRequestSmsJob>(job => job.Send(requestId, CancellationToken.None), at);
}

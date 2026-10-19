using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each welcome as a Hangfire background job (BUSINESS_RULES.md §10 <i>The welcome</i>): enqueued for
/// now, or scheduled for 10:00, kept in Hangfire's own tables like the receipts.
/// </summary>
public sealed class HangfireWelcomeScheduler(IBackgroundJobClient jobs) : IWelcomeScheduler
{
    public void SendNow(Guid subscriptionId) =>
        jobs.Enqueue<WelcomeJob>(job => job.Send(subscriptionId, CancellationToken.None));

    public void SendAt(Guid subscriptionId, DateTimeOffset at) =>
        jobs.Schedule<WelcomeJob>(job => job.Send(subscriptionId, CancellationToken.None), at);
}

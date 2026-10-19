using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>Each broadcast as a Hangfire background job (BUSINESS_RULES.md §10 <i>The broadcast</i>).</summary>
public sealed class HangfireBroadcastScheduler(IBackgroundJobClient jobs) : IBroadcastScheduler
{
    public void SendNow(Guid broadcastId) =>
        jobs.Enqueue<BroadcastJob>(job => job.Send(broadcastId, CancellationToken.None));
}

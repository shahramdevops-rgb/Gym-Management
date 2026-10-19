using Gym.Application.Common.Sms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Each receipt as a Hangfire background job (BUSINESS_RULES.md §10 <i>The subscription receipt</i>):
/// enqueued for now, or scheduled for 10:00. Hangfire keeps both in its own tables, so a receipt
/// waiting for the morning outlives a restart.
/// </summary>
public sealed class HangfireSubscriptionReceiptScheduler(IBackgroundJobClient jobs) : ISubscriptionReceiptScheduler
{
    public void SendNow(Guid subscriptionId) =>
        jobs.Enqueue<SubscriptionReceiptJob>(job => job.Send(subscriptionId, CancellationToken.None));

    public void SendAt(Guid subscriptionId, DateTimeOffset at) =>
        jobs.Schedule<SubscriptionReceiptJob>(job => job.Send(subscriptionId, CancellationToken.None), at);
}

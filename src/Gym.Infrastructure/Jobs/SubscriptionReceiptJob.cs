using Gym.Application.Notifications.SendSubscriptionReceipt;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send one plan's receipt (BUSINESS_RULES.md §10 <i>The subscription
/// receipt</i>). A thin wrapper, so the Hangfire attributes stay in this layer and the sending in
/// Application.
/// </summary>
public sealed class SubscriptionReceiptJob(SendSubscriptionReceiptHandler handler)
{
    /// <summary>Longer than the longest send: its retries wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <para>
    /// <b>One receipt at a time</b>: <see cref="DisableConcurrentExecutionAttribute"/> locks on this
    /// method, whatever the subscription. That is what lets the handler treat a receipt it finds
    /// <c>Pending</c> as left over from a job that stopped.
    /// </para>
    /// <para>
    /// <b>A few automatic retries are safe</b>: a job that failed before its row was written simply
    /// tries again, and one that failed after finds its row <c>Pending</c> and marks it <c>Unknown</c>
    /// rather than sending it twice.
    /// </para>
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 3)]
    public Task Send(Guid subscriptionId, CancellationToken cancellationToken) =>
        handler.Handle(subscriptionId, cancellationToken);
}

using Gym.Application.Notifications.SendWelcome;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send one member's welcome (BUSINESS_RULES.md §10 <i>The welcome</i>). A thin
/// wrapper, as <see cref="SubscriptionReceiptJob"/> is.
/// </summary>
public sealed class WelcomeJob(SendWelcomeHandler handler)
{
    /// <summary>Longer than the longest send: its retries wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>One welcome at a time</b>, which lets the handler treat a welcome it finds <c>Pending</c> as
    /// left over from a job that stopped; and a few automatic retries, safe for the same reason as the
    /// receipt's (<see cref="SubscriptionReceiptJob.Send"/>).
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 3)]
    public Task Send(Guid subscriptionId, CancellationToken cancellationToken) =>
        handler.Handle(subscriptionId, cancellationToken);
}

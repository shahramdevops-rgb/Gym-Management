using Gym.Application.Notifications.Broadcasts.RunBroadcast;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send a broadcast (BUSINESS_RULES.md §10 <i>The broadcast</i>). A thin
/// wrapper, as <see cref="WelcomeJob"/> is.
/// </summary>
public sealed class BroadcastJob(RunBroadcastHandler handler)
{
    /// <summary>Longer than the longest broadcast: hundreds of requests, then retries that wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>One broadcast at a time</b>, which lets the handler treat a message of it found
    /// <c>Pending</c> as left over from a job that stopped. <b>No automatic retry</b>: an unexpected
    /// failure stops the broadcast, and the Owner continues it by hand («ادامه ارسال»). A job the
    /// server stopped in the middle is still made again by Hangfire once the server is back, and
    /// continues with whoever has no message yet.
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 0)]
    public Task Send(Guid broadcastId, CancellationToken cancellationToken) =>
        handler.Handle(broadcastId, cancellationToken);
}

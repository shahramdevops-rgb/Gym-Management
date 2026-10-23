using Gym.Application.Notifications.ResendCancelledSms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls every 15 minutes to send again what the provider cancelled (BUSINESS_RULES.md
/// §10 <i>Sending</i>, task 10.16). A thin wrapper, as <see cref="DailySmsJob"/> is.
/// </summary>
public sealed class CancelledSmsResendJob(ResendCancelledSmsHandler handler)
{
    /// <summary>Less than the 15 minutes to the next one: a run still waiting by then is left to it.</summary>
    public const int LockTimeoutSeconds = 600;

    /// <remarks>
    /// <b>Never two at once</b>, so one cancelled message is not picked up by both. <b>No automatic
    /// retry</b>: the next one comes in 15 minutes and finds whatever is still cancelled.
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 0)]
    public Task Run(CancellationToken cancellationToken) =>
        handler.Handle(cancellationToken);
}

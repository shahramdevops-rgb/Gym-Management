using Gym.Application.Notifications.SendDailySms;
using Gym.Domain.Notifications;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls at a kind's send time (BUSINESS_RULES.md §10 <i>The daily runs</i>). A thin
/// wrapper, so the Hangfire attributes stay in this layer and the run itself in Application.
/// </summary>
public sealed class DailySmsJob(SendDailySmsHandler handler)
{
    /// <summary>
    /// Longer than the longest run (its retries wait 6 minutes in all), so a run that has to wait for
    /// another kind's to end still gets its turn.
    /// </summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>Never two at once</b>, of any kind: <see cref="DisableConcurrentExecutionAttribute"/> locks on
    /// this method, whatever the kind. That is what lets the run treat a row it finds <c>Pending</c> as
    /// left over from a stopped run, and keeps two kinds from meeting an empty account together.
    /// A run Hangfire missed while the app was down is made once when it is back; the handler sends
    /// nothing if that is after 22:00 (§0).
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    public Task Run(NotificationKind kind, CancellationToken cancellationToken) =>
        handler.Handle(kind, cancellationToken);
}

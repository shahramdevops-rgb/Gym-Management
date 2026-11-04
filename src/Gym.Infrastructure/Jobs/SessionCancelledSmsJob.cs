using Gym.Application.Notifications.SendSessionCancelledSms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send one student's SMS about a cancelled session (BUSINESS_RULES.md §15,
/// task 15.6c). A thin wrapper, as <see cref="TrainerRequestSmsJob"/> is.
/// </summary>
public sealed class SessionCancelledSmsJob(SendSessionCancelledSmsHandler handler)
{
    /// <summary>Longer than the longest send: its retries wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>One at a time</b>, which lets the handler treat a row it finds <c>Pending</c> as left over
    /// from a job that stopped; and a few automatic retries, safe for the same reason as the trainer's.
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 3)]
    public Task Send(Guid changeId, Guid memberId, CancellationToken cancellationToken) =>
        handler.Handle(changeId, memberId, cancellationToken);
}

using Gym.Application.Notifications.SendWelcomeBack;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send one member's welcome back (BUSINESS_RULES.md §10 <i>The welcome
/// back</i>). A thin wrapper, as <see cref="WelcomeJob"/> is.
/// </summary>
public sealed class WelcomeBackJob(SendWelcomeBackHandler handler)
{
    /// <summary>Longer than the longest send: its retries wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>One welcome back at a time</b>, which lets the handler treat one it finds <c>Pending</c> as
    /// left over from a job that stopped; and a few automatic retries, safe for the same reason as the
    /// welcome's (<see cref="WelcomeJob.Send"/>).
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 3)]
    public Task Send(Guid attendanceId, CancellationToken cancellationToken) =>
        handler.Handle(attendanceId, cancellationToken);
}

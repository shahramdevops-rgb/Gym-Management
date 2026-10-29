using Gym.Application.Notifications.SendTrainerRequestSms;

using Hangfire;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// What Hangfire calls to send one trainer's SMS about a request (BUSINESS_RULES.md §15, task 15.4c).
/// A thin wrapper, as <see cref="WelcomeJob"/> is.
/// </summary>
public sealed class TrainerRequestSmsJob(SendTrainerRequestSmsHandler handler)
{
    /// <summary>Longer than the longest send: its retries wait 6 minutes in all.</summary>
    public const int LockTimeoutSeconds = 3600;

    /// <remarks>
    /// <b>One at a time</b>, which lets the handler treat a row it finds <c>Pending</c> as left over
    /// from a job that stopped; and a few automatic retries, safe for the same reason as the welcome's.
    /// </remarks>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    [AutomaticRetry(Attempts = 3)]
    public Task Send(Guid requestId, CancellationToken cancellationToken) =>
        handler.Handle(requestId, cancellationToken);
}

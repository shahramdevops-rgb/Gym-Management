using Gym.Application.Notifications.Broadcasts.RecoverBroadcasts;

using Hangfire.States;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// When a <see cref="BroadcastJob"/> fails, stops its broadcast if it is still sending
/// (<see cref="RecoverBroadcastsHandler.StopAfterJobFailedAsync"/>), so it does not hold the one
/// sending place. The handler already stops it on its own failures; this covers a job that failed
/// before the handler ran (it could not be made, or waited too long for the one-at-a-time lock).
/// </summary>
public sealed partial class StopBroadcastOnFailureFilter(IServiceScopeFactory scopes) : IElectStateFilter
{
    public void OnStateElection(ElectStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateState is not FailedState
            || context.BackgroundJob.Job?.Type != typeof(BroadcastJob)
            || context.BackgroundJob.Job.Args is not [Guid broadcastId, ..])
        {
            return;
        }

        // Hangfire's filters are synchronous; this runs on a worker thread with no synchronization context.
        using var scope = scopes.CreateScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<RecoverBroadcastsHandler>()
                .StopAfterJobFailedAsync(broadcastId, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never break the job's own move to Failed; the next start hands it to the job again.
            LogFailed(scope.ServiceProvider.GetRequiredService<ILogger<StopBroadcastOnFailureFilter>>(), exception, broadcastId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Broadcast {BroadcastId}: could not stop it after its job failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid broadcastId);
}

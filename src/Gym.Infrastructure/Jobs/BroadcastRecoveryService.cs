using Gym.Application.Notifications.Broadcasts.RecoverBroadcasts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// At the server's start, hands a broadcast still «در حال ارسال» to its job again
/// (<see cref="RecoverBroadcastsHandler"/>). The database is migrated before the server starts
/// (deploy/server.sh), so it can be read here.
/// </summary>
/// <remarks>
/// A failure is logged, never thrown: the panel must start even if this cannot run. The integration
/// tests leave it out (GymApiFactory), since their host starts before the test database is migrated.
/// </remarks>
public sealed partial class BroadcastRecoveryService(
    IServiceScopeFactory scopes,
    ILogger<BroadcastRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RecoverBroadcastsHandler>().RequeueSendingAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not check for a broadcast still sending at start")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.Broadcasts.RecoverBroadcasts;

/// <summary>
/// Keeps a broadcast from staying «در حال ارسال» with no job behind it (BUSINESS_RULES.md §10 <i>The
/// broadcast</i>): while one is sending, no other can start, so a stuck one would block them all.
/// </summary>
/// <remarks>
/// <para>
/// <b>At the server's start</b>, one still sending is handed to the job again: the server may have
/// stopped between saving it and queueing its job. A second job for it is harmless: the jobs run one
/// at a time, and a job that finds it no longer sending does nothing.
/// </para>
/// <para>
/// <b>When its job fails</b> before the handler could stop it (the job could not be started at all),
/// it is stopped with <see cref="BroadcastStopReason.Error"/>, so the Owner can continue it.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs calls it at start and from Hangfire.
/// </para>
/// </remarks>
public sealed partial class RecoverBroadcastsHandler(
    IAppDbContext db,
    TimeProvider time,
    BroadcastSending sending,
    ILogger<RecoverBroadcastsHandler> logger)
{
    /// <summary>Hands every broadcast still sending to the job again; how many there were.</summary>
    public async Task<int> RequeueSendingAsync(CancellationToken cancellationToken)
    {
        var stillSending = await db.SmsBroadcasts
            .Where(broadcast => broadcast.Status == BroadcastStatus.Sending)
            .ToListAsync(cancellationToken);

        foreach (var broadcast in stillSending)
        {
            LogRequeued(logger, broadcast.Id);
            await sending.StartAsync(broadcast);
        }

        return stillSending.Count;
    }

    /// <summary>Stops it with <see cref="BroadcastStopReason.Error"/> if it is still sending.</summary>
    public async Task StopAfterJobFailedAsync(Guid broadcastId, CancellationToken cancellationToken)
    {
        var broadcast = await db.SmsBroadcasts.SingleOrDefaultAsync(row => row.Id == broadcastId, cancellationToken);
        if (broadcast is null || broadcast.Status != BroadcastStatus.Sending)
        {
            return;
        }

        broadcast.Stop(BroadcastStopReason.Error, time.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            LogStopped(logger, broadcastId);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed meanwhile (stopped or continued by someone else): theirs stands.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast {BroadcastId} was still sending at start; handed to its job again")]
    private static partial void LogRequeued(ILogger logger, Guid broadcastId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Broadcast {BroadcastId}: its job failed; it is stopped")]
    private static partial void LogStopped(ILogger logger, Guid broadcastId);
}

using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.Broadcasts;

/// <summary>
/// What sending a broadcast and continuing one share (BUSINESS_RULES.md §10 <i>The broadcast</i>):
/// the checks before it may go, and handing it to the job.
/// </summary>
public sealed partial class BroadcastSending(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    IBroadcastScheduler scheduler,
    ILogger<BroadcastSending> logger)
{
    private const NotificationKind Kind = NotificationKind.Broadcast;

    /// <summary>The switches, then the hours: <c>null</c> when a broadcast may go now.</summary>
    public async Task<Error?> CheckCanSendAsync(CancellationToken cancellationToken)
    {
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.BroadcastEnabled)
        {
            return SmsBroadcastErrors.Off;
        }

        return SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay())
            ? null
            : NotificationErrors.OutsideSendingHours;
    }

    /// <summary>
    /// Whether one is sending now. The partial unique index is what really holds one at a time; this
    /// only answers before the save, with the same code.
    /// </summary>
    public Task<bool> AnySendingAsync(CancellationToken cancellationToken) =>
        db.SmsBroadcasts.AnyAsync(broadcast => broadcast.Status == BroadcastStatus.Sending, cancellationToken);

    /// <summary>
    /// Hands the saved broadcast to the job. If the job runner cannot take it, the broadcast is
    /// stopped at once, so it does not hold the one sending place, and the Owner can continue it.
    /// </summary>
    public async Task StartAsync(SmsBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        try
        {
            scheduler.SendNow(broadcast.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotQueued(logger, exception, broadcast.Id);
            broadcast.Stop(BroadcastStopReason.Error, time.GetUtcNow());
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Broadcast {BroadcastId} could not be queued; it is stopped")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid broadcastId);
}

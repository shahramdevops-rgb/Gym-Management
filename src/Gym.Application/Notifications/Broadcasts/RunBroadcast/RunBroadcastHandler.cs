using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.Broadcasts.RunBroadcast;

/// <summary>
/// Writes and sends a broadcast's messages (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13),
/// asked for by the send and by «ادامه ارسال».
/// </summary>
/// <remarks>
/// <para>
/// <b>The group is read when the job starts</b>, leaving out whoever has a message of this broadcast
/// already, so a job made again after a restart, or a broadcast continued, reaches only the rest. The
/// unique index on the broadcast and the member is what really keeps anyone from being paid for twice.
/// </para>
/// <para>
/// <b>Written, then sent, as a daily run</b> (<see cref="SmsBatchSender"/>): retries until 22:00, and
/// credit used up stops it. A broadcast that stops is <see cref="BroadcastStatus.Stopped"/> with its
/// reason, and the Owner can continue it.
/// </para>
/// <para>
/// <b>Checked again here, and before every message.</b> The job may run later than it was asked for
/// (a restart), and a long one may reach 22:00 or meet a switch the Owner turned off: the switches and
/// the hours are read before each message, and a job that finds them closed stops the broadcast with
/// that reason rather than wait or go on. The text is measured again against the group as it is
/// now, so no message goes out longer than 3 parts.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one broadcast at a time.
/// </para>
/// </remarks>
public sealed partial class RunBroadcastHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    SmsBatchSender batch,
    BroadcastRecipients recipients,
    ILogger<RunBroadcastHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.Broadcast;

    public async Task<BroadcastRunResult> Handle(Guid broadcastId, CancellationToken cancellationToken)
    {
        var broadcast = await db.SmsBroadcasts.SingleOrDefaultAsync(broadcast => broadcast.Id == broadcastId, cancellationToken);
        if (broadcast is null || broadcast.Status != BroadcastStatus.Sending)
        {
            LogNotRun(logger, broadcastId);
            return BroadcastRunResult.NotRun;
        }

        try
        {
            return await RunAsync(broadcast, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Stopped, so it does not hold the one sending place; the Owner can continue it. Whatever
            // the failed save left tracked is dropped first, so this save is only the stop.
            LogFailed(logger, exception, broadcastId);
            db.ChangeTracker.Clear();
            var fresh = await db.SmsBroadcasts.SingleAsync(row => row.Id == broadcastId, CancellationToken.None);
            if (fresh.Status == BroadcastStatus.Sending)
            {
                await StopAsync(fresh, BroadcastStopReason.Error);
            }

            throw;
        }
    }

    private async Task<BroadcastRunResult> RunAsync(SmsBroadcast broadcast, CancellationToken cancellationToken)
    {
        var interrupted = await MarkInterruptedAsync(broadcast.Id, cancellationToken);

        if (await ClosedForAsync(cancellationToken) is { } closed)
        {
            return await StopAsync(broadcast, closed, interrupted);
        }

        var rest = await recipients.FindAsync(broadcast.Audience, calendar.Today(), broadcast.Id, cancellationToken);

        // Measured at the send, but the group is read again now: a longer first name may have joined.
        if (rest.Count > 0 && SmsBroadcast.CheckLength(broadcast.Text, BroadcastRecipients.LongestFirstName(rest)) is not null)
        {
            return await StopAsync(broadcast, BroadcastStopReason.TooLong, interrupted);
        }
        var messages = rest.Select(recipient => Notification.ForBroadcast(
            broadcast.Id,
            recipient.MemberId,
            recipient.PhoneNumber,
            SmsText.ForBroadcast(recipient.FullName, broadcast.Text)));

        BroadcastStopReason? haltedFor = null;
        var sent = await batch.SendAsync(
            Kind,
            messages,
            async ct =>
            {
                haltedFor = await ClosedForAsync(ct);
                return haltedFor is null;
            },
            cancellationToken);

        if (sent.CreditExhausted)
        {
            broadcast.Stop(BroadcastStopReason.CreditExhausted, time.GetUtcNow());
        }
        else if (sent.Halted && haltedFor is { } reason)
        {
            broadcast.Stop(reason, time.GetUtcNow());
        }
        else
        {
            broadcast.Finish(time.GetUtcNow());
        }

        await db.SaveChangesAsync(CancellationToken.None);

        var result = new BroadcastRunResult(Ran: true, broadcast.Status, sent.Sent, sent.Failed, sent.Unknown, interrupted);
        LogRun(logger, broadcast.Id, broadcast.Status, sent.Sent, sent.Failed, sent.Unknown, interrupted);

        return result;
    }

    /// <summary>
    /// Why it may not send now: all SMS or the broadcast turned off, or outside 08:00–22:00; <c>null</c>
    /// when it may. Read fresh each time (not tracked), so a switch the Owner just saved counts.
    /// </summary>
    private async Task<BroadcastStopReason?> ClosedForAsync(CancellationToken cancellationToken)
    {
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.BroadcastEnabled)
        {
            return BroadcastStopReason.TurnedOff;
        }

        return SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay())
            ? null
            : BroadcastStopReason.OutsideSendingHours;
    }

    /// <summary>
    /// Its messages a stopped job left <c>Pending</c>: whether their request left is not known, so
    /// they are <c>Unknown</c>, never sent again by itself. One broadcast runs at a time, so any found
    /// now are left over.
    /// </summary>
    private async Task<int> MarkInterruptedAsync(Guid broadcastId, CancellationToken cancellationToken)
    {
        var leftOver = await db.Notifications
            .Where(notification => notification.BroadcastId == broadcastId && notification.Status == NotificationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var notification in leftOver)
        {
            notification.MarkInterrupted();
        }

        if (leftOver.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return leftOver.Count;
    }

    private async Task<BroadcastRunResult> StopAsync(SmsBroadcast broadcast, BroadcastStopReason reason, int interrupted = 0)
    {
        broadcast.Stop(reason, time.GetUtcNow());
        await db.SaveChangesAsync(CancellationToken.None);
        LogStopped(logger, broadcast.Id, reason);

        return new BroadcastRunResult(Ran: true, broadcast.Status, Interrupted: interrupted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast {BroadcastId} not run: not found or no longer sending")]
    private static partial void LogNotRun(ILogger logger, Guid broadcastId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast {BroadcastId} stopped: {Reason}")]
    private static partial void LogStopped(ILogger logger, Guid broadcastId, BroadcastStopReason reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Broadcast {BroadcastId} failed; it is stopped")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid broadcastId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Broadcast {BroadcastId}: {Status}, {Sent} sent, {Failed} failed, {Unknown} unknown, {Interrupted} left over from a stopped job")]
    private static partial void LogRun(
        ILogger logger, Guid broadcastId, BroadcastStatus status, int sent, int failed, int unknown, int interrupted);
}

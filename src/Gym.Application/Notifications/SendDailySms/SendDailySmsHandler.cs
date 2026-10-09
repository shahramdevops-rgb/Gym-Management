using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// One kind's daily SMS run (BUSINESS_RULES.md §10 <i>The daily runs</i>): finds who is due the
/// message today and sends it to each, once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, then sent, one at a time.</b> Each message is saved <c>Pending</c> before its request
/// and saved again with the outcome after it. The unique indexes refuse a second row for the same
/// event, so a second run, or a run racing this one, cannot pay for it again. If the app stops between
/// the two saves, the next run of the kind finds the row still <c>Pending</c> and marks it
/// <c>Unknown</c>: the request may have left, and it carries no duplicate guard.
/// </para>
/// <para>
/// <b>Retries come after everyone else.</b> A failure that may pass waits for the round of retries,
/// so one busy moment at the provider does not hold up the rest. Each round waits the configured
/// delay (1 minute, then 5) on the injected clock; a round that would fall after 22:00 is not made.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire at the kind's send time,
/// and never two runs at once.
/// </para>
/// </remarks>
public sealed partial class SendDailySmsHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    ILogger<SendDailySmsHandler> logger)
{
    public async Task<SmsRunResult> Handle(NotificationKind kind, CancellationToken cancellationToken)
    {
        // Read now, not when the run was scheduled: a change applies from the next run (§10).
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.For(kind).Enabled)
        {
            LogNotRun(logger, kind, "SMS or this kind is off");
            return SmsRunResult.NotRun;
        }

        if (!SmsSettings.IsWithinSendingHours(calendar.TimeOfDay()))
        {
            LogNotRun(logger, kind, "outside the sending hours");
            return SmsRunResult.NotRun;
        }

        var tally = new Tally { Interrupted = await MarkInterruptedAsync(kind, cancellationToken) };

        var today = calendar.Today();
        var candidates = await SmsCandidates.FindAsync(db, kind, settings, today, cancellationToken);

        var waiting = new List<Notification>();
        foreach (var notification in candidates)
        {
            if (!await TryWriteAsync(notification, cancellationToken))
            {
                continue;
            }

            var outcome = await SendAsync(notification, tally, cancellationToken);
            if (outcome == SmsSendOutcome.CreditExhausted)
            {
                // §10: the rest of the run is not sent; the ones not written yet stay unwritten, so
                // tomorrow's run still reaches whoever is still covered.
                await GiveUpAsync(waiting, tally);
                return tally.Finish(kind, logger);
            }

            if (outcome == SmsSendOutcome.RetryableFailure)
            {
                waiting.Add(notification);
            }
        }

        await RetryAsync(waiting, tally, cancellationToken);

        return tally.Finish(kind, logger);
    }

    /// <summary>Rows a stopped run left <c>Pending</c>. Runs never overlap, so any found now are left over.</summary>
    private async Task<int> MarkInterruptedAsync(NotificationKind kind, CancellationToken cancellationToken)
    {
        var leftOver = await db.Notifications
            .Where(notification => notification.Kind == kind && notification.Status == NotificationStatus.Pending)
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

    /// <summary>Saves the row before its request. <c>false</c> when the event already has one.</summary>
    private async Task<bool> TryWriteAsync(Notification notification, CancellationToken cancellationToken)
    {
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (UniqueConstraintException)
        {
            // Another run wrote it between the query and here. Removing an added entity only stops
            // tracking it, so the next save does not try to write it again.
            db.Notifications.Remove(notification);
            return false;
        }
    }

    /// <summary>One request, and its outcome saved on the row.</summary>
    private async Task<SmsSendOutcome> SendAsync(Notification notification, Tally tally, CancellationToken cancellationToken)
    {
        var message = new SmsMessage(notification.Recipient, notification.Text);

        // Not the run's token: once the request may have left, its outcome is recorded whatever happens.
        var result = await sender.SendAsync(message, CancellationToken.None);
        var now = time.GetUtcNow();

        switch (result.Outcome)
        {
            case SmsSendOutcome.Sent:
                notification.MarkSent(
                    result.ProviderMessageId ?? throw new InvalidOperationException("A sent message has no provider id."),
                    result.CostRial ?? 0m,
                    now);
                tally.Sent++;
                break;

            case SmsSendOutcome.RetryableFailure:
                notification.RecordRetryableFailure(result.ErrorCode, now);
                break;

            case SmsSendOutcome.PermanentFailure:
                notification.MarkFailed(result.ErrorCode, now);
                tally.Failed++;
                break;

            case SmsSendOutcome.CreditExhausted:
                notification.MarkFailed(result.ErrorCode, now);
                tally.Failed++;
                tally.CreditExhausted = true;
                break;

            case SmsSendOutcome.Unknown:
                notification.MarkUnknown(now);
                tally.Unknown++;
                break;

            default:
                throw new InvalidOperationException($"Unknown send outcome {result.Outcome}.");
        }

        await db.SaveChangesAsync(CancellationToken.None);

        return result.Outcome;
    }

    /// <summary>Rounds of retries until each one is sent, fails for good, or runs out of tries or time.</summary>
    private async Task RetryAsync(List<Notification> waiting, Tally tally, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        while (waiting.Count > 0)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay()) is not { } delay)
            {
                await GiveUpAsync(waiting, tally);
                return;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;

            var stillWaiting = new List<Notification>();
            for (var i = 0; i < waiting.Count; i++)
            {
                var outcome = await SendAsync(waiting[i], tally, cancellationToken);
                if (outcome == SmsSendOutcome.CreditExhausted)
                {
                    // The rest would meet the same empty account.
                    await GiveUpAsync([.. stillWaiting, .. waiting.Skip(i + 1)], tally);
                    return;
                }

                if (outcome == SmsSendOutcome.RetryableFailure)
                {
                    stillWaiting.Add(waiting[i]);
                }
            }

            waiting = stillWaiting;
        }
    }

    /// <summary><c>Failed</c> without another request, keeping the last failure's code.</summary>
    private async Task GiveUpAsync(List<Notification> notifications, Tally tally)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            notification.GiveUp();
            tally.Failed++;
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class Tally
    {
        public int Sent { get; set; }

        public int Failed { get; set; }

        public int Unknown { get; set; }

        public int Interrupted { get; set; }

        public bool CreditExhausted { get; set; }

        public SmsRunResult Finish(NotificationKind kind, ILogger logger)
        {
            LogRun(logger, kind, Sent, Failed, Unknown, Interrupted, CreditExhausted);

            return new SmsRunResult(Ran: true, Sent, Failed, Unknown, Interrupted, CreditExhausted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS run {Kind} not made: {Reason}")]
    private static partial void LogNotRun(ILogger logger, NotificationKind kind, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMS run {Kind}: {Sent} sent, {Failed} failed, {Unknown} unknown, {Interrupted} left over from a stopped run, credit used up: {CreditExhausted}")]
    private static partial void LogRun(
        ILogger logger, NotificationKind kind, int sent, int failed, int unknown, int interrupted, bool creditExhausted);
}

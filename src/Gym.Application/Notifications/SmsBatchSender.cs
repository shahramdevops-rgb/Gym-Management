using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications;

/// <summary>
/// Writes and sends a batch of messages one at a time, as a daily run does (BUSINESS_RULES.md §10
/// <i>The daily runs</i>). Shared by the daily runs and the broadcast (task 10.13), so both treat a
/// failure, a retry and an empty account the same way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, then sent.</b> Each message is saved <c>Pending</c> before its request and saved again
/// with the outcome after it. The unique indexes refuse a second row for the same event, so a second
/// batch, or one racing this one, cannot pay for it again: such a message is skipped.
/// </para>
/// <para>
/// <b>Retries come after everyone else.</b> A failure that may pass waits for the round of retries,
/// so one busy moment at the provider does not hold up the rest. Each round waits the configured
/// delay (1 minute, then 5) on the injected clock; a round that would fall after the kind's last
/// sending moment is not made.
/// </para>
/// <para>
/// <b>Credit used up stops the batch</b>: the messages not written yet stay unwritten, and those
/// waiting for a retry are <c>Failed</c>, since they would meet the same empty account.
/// </para>
/// <para>
/// <b>A caller may halt it</b> with a check asked before each new message and each round of retries
/// (the broadcast: the hours ended, or a switch turned off). Halted, it stops as on an empty account.
/// The daily runs pass none and go to the end.
/// </para>
/// </remarks>
public sealed class SmsBatchSender(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries)
{
    /// <param name="kind">Whose sending hours the retries keep to.</param>
    /// <param name="candidates">The messages, built but not saved, in the order they are to go.</param>
    public Task<SmsBatchResult> SendAsync(
        NotificationKind kind, IEnumerable<Notification> candidates, CancellationToken cancellationToken) =>
        SendAsync(kind, candidates, mayGoOn: null, cancellationToken);

    /// <param name="kind">Whose sending hours the retries keep to.</param>
    /// <param name="candidates">The messages, built but not saved, in the order they are to go.</param>
    /// <param name="mayGoOn">
    /// Asked before each new message and each round of retries; <c>false</c> halts the batch
    /// (<see cref="SmsBatchResult.Halted"/>). <c>null</c>: it never halts.
    /// </param>
    public async Task<SmsBatchResult> SendAsync(
        NotificationKind kind,
        IEnumerable<Notification> candidates,
        Func<CancellationToken, Task<bool>>? mayGoOn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var tally = new Tally();
        var waiting = new List<Notification>();
        foreach (var notification in candidates)
        {
            if (mayGoOn is not null && !await mayGoOn(cancellationToken))
            {
                tally.Halted = true;
                await GiveUpAsync(waiting, tally);
                return tally.Result();
            }

            if (!await TryWriteAsync(notification, cancellationToken))
            {
                continue;
            }

            var outcome = await SendAsync(notification, tally);
            if (outcome == SmsSendOutcome.CreditExhausted)
            {
                await GiveUpAsync(waiting, tally);
                return tally.Result();
            }

            if (outcome == SmsSendOutcome.RetryableFailure)
            {
                waiting.Add(notification);
            }
        }

        await RetryAsync(kind, waiting, mayGoOn, tally, cancellationToken);

        return tally.Result();
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
            // Another batch wrote it between the query and here. Removing an added entity only stops
            // tracking it, so the next save does not try to write it again.
            db.Notifications.Remove(notification);
            return false;
        }
    }

    /// <summary>One request, its outcome saved on the row (<see cref="SmsRequest"/>) and counted.</summary>
    private async Task<SmsSendOutcome> SendAsync(Notification notification, Tally tally)
    {
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        switch (outcome)
        {
            case SmsSendOutcome.Sent:
                tally.Sent++;
                break;

            case SmsSendOutcome.PermanentFailure:
                tally.Failed++;
                break;

            case SmsSendOutcome.CreditExhausted:
                tally.Failed++;
                tally.CreditExhausted = true;
                break;

            case SmsSendOutcome.Unknown:
                tally.Unknown++;
                break;
        }

        return outcome;
    }

    /// <summary>Rounds of retries until each one is sent, fails for good, or runs out of tries or time.</summary>
    private async Task RetryAsync(
        NotificationKind kind,
        List<Notification> waiting,
        Func<CancellationToken, Task<bool>>? mayGoOn,
        Tally tally,
        CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        while (waiting.Count > 0)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), kind) is not { } delay)
            {
                await GiveUpAsync(waiting, tally);
                return;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;

            if (mayGoOn is not null && !await mayGoOn(cancellationToken))
            {
                tally.Halted = true;
                await GiveUpAsync(waiting, tally);
                return;
            }

            var stillWaiting = new List<Notification>();
            for (var i = 0; i < waiting.Count; i++)
            {
                var outcome = await SendAsync(waiting[i], tally);
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

        public bool CreditExhausted { get; set; }

        public bool Halted { get; set; }

        public SmsBatchResult Result() => new(Sent, Failed, Unknown, CreditExhausted, Halted);
    }
}

/// <summary>What became of a batch's messages.</summary>
/// <param name="CreditExhausted">The provider said the credit is used up; the rest were not written.</param>
/// <param name="Halted">The caller's check said no more; the rest were not written.</param>
public sealed record SmsBatchResult(int Sent, int Failed, int Unknown, bool CreditExhausted, bool Halted = false);

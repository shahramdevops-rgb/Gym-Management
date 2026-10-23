using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Payments;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendSubscriptionReceipt;

/// <summary>
/// Sends one plan's receipt (BUSINESS_RULES.md §10 <i>The subscription receipt</i>, task 10.9),
/// asked for by <see cref="SubscriptionReceipts"/> when a payment made it fully paid.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is checked again here.</b> The job may run hours after the payment (a payment at
/// 23:00 is sent at 10:00), and by then the Owner may have turned the receipt off, the plan may have
/// been cancelled, or the member deactivated.
/// </para>
/// <para>
/// <b>Written, then sent, like a daily run.</b> The row is saved <c>Pending</c> before the request,
/// and the unique index on the subscription and the kind refuses a second one. If the app stops
/// between the two saves, Hangfire makes the job again once the app is back; it then finds the row
/// still <c>Pending</c> and marks it <c>Unknown</c>, since the request may have left.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one receipt at a time.
/// </para>
/// </remarks>
public sealed partial class SendSubscriptionReceiptHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    SubscriptionReceipts receipts,
    ILogger<SendSubscriptionReceiptHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.SubscriptionReceipt;

    public async Task<ReceiptOutcome> Handle(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var outcome = await SendAsync(subscriptionId, cancellationToken);
        LogOutcome(logger, subscriptionId, outcome);

        return outcome;
    }

    private async Task<ReceiptOutcome> SendAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        // Receipts run one at a time, so a row found Pending was left by a job that stopped.
        var written = await db.Notifications
            .SingleOrDefaultAsync(
                notification => notification.SubscriptionId == subscriptionId && notification.Kind == Kind,
                cancellationToken);
        if (written is not null)
        {
            if (written.MarkInterrupted().IsFailure)
            {
                return ReceiptOutcome.AlreadyWritten;
            }

            await db.SaveChangesAsync(cancellationToken);
            return ReceiptOutcome.Interrupted;
        }

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.SubscriptionReceiptEnabled)
        {
            return ReceiptOutcome.Off;
        }

        // A job whose time came while the server was down may run at night: it waits for 10:00.
        if (!SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay()))
        {
            receipts.Queue(subscriptionId);
            return ReceiptOutcome.Deferred;
        }

        if (await BuildAsync(subscriptionId, cancellationToken) is not { } notification)
        {
            return ReceiptOutcome.NoLongerDue;
        }

        if (!await TryWriteAsync(notification, cancellationToken))
        {
            return ReceiptOutcome.AlreadyWritten;
        }

        return await SendWithRetriesAsync(notification, cancellationToken);
    }

    /// <summary>The message, or <c>null</c> when the receipt is no longer due.</summary>
    private async Task<Notification?> BuildAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.AsNoTracking()
            .SingleOrDefaultAsync(subscription => subscription.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var member = await db.Members.AsNoTracking()
            .Where(member => member.Id == subscription.MemberId)
            .Select(member => new { member.FullName, member.PhoneNumber, member.IsActive })
            .SingleAsync(cancellationToken);
        if (!member.IsActive)
        {
            return null;
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, subscriptionId, cancellationToken);
        if (!SmsAudience.ReceiptStillDue(subscription, netPaid))
        {
            return null;
        }

        // Whether it waits behind another plan is read today, as the member's history would show it.
        var today = calendar.Today();
        var otherPlans = await db.Subscriptions.AsNoTracking()
            .Where(other => other.MemberId == subscription.MemberId
                && other.Id != subscription.Id
                && !other.IsSingleSession
                && other.CancelledAt == null)
            .ToListAsync(cancellationToken);

        var text = SmsAudience.IsQueuedBehindAnother(subscription, otherPlans, today)
            ? SmsText.ForQueuedSubscriptionReceipt(
                member.FullName, subscription.TotalSessions, subscription.DurationDays, subscription.Price)
            : SmsText.ForSubscriptionReceipt(
                member.FullName, subscription.TotalSessions, subscription.StartDate, subscription.EndDate, subscription.Price);

        return Notification.ForSubscriptionReceipt(subscription.MemberId, subscription.Id, member.PhoneNumber, text);
    }

    /// <summary>Saves the row before its request. <c>false</c> when the subscription already has one.</summary>
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
            db.Notifications.Remove(notification);
            return false;
        }
    }

    /// <summary>A failure that may pass is tried again, as in a run, while the tries and the hours last.</summary>
    private async Task<ReceiptOutcome> SendWithRetriesAsync(Notification notification, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        while (outcome == SmsSendOutcome.RetryableFailure)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), Kind) is not { } delay)
            {
                notification.GiveUp();
                await db.SaveChangesAsync(CancellationToken.None);
                return ReceiptOutcome.Failed;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;
            outcome = await SmsRequest.SendAsync(db, sender, time, notification);
        }

        return outcome switch
        {
            SmsSendOutcome.Sent => ReceiptOutcome.Sent,
            SmsSendOutcome.Unknown => ReceiptOutcome.Unknown,
            _ => ReceiptOutcome.Failed,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Receipt of subscription {SubscriptionId}: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid subscriptionId, ReceiptOutcome outcome);
}

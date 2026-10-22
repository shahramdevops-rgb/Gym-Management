using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.CheckSmsDelivery;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.ResendCancelledSms;

/// <summary>
/// The automatic resend of what the provider cancelled (BUSINESS_RULES.md §10 <i>Sending</i>, task
/// 10.16): every 15 minutes it asks the provider about today's messages, and sends each one it
/// cancelled again, at least 30 minutes after its last request, at most five times, and only the
/// same day.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> Kavenegar accepted a whole evening's messages and then cancelled them (13, the cost
/// given back), two nights running (1405/07/29 and 07/30), while the ones before and after went
/// through. A cancelled message never reached the phone and cost nothing, so sending it again later
/// is safe; 30 minutes apart, so the tries are not all spent inside the same bad stretch (decided
/// with the developer, 1405/08/01).
/// </para>
/// <para>
/// <b>Each one like the Owner's resend</b>: saved <c>Pending</c> before its one request, and its
/// outcome saved after (<see cref="SmsResendOutcome"/>). Two writers cannot both pass the first save
/// (<c>xmin</c>), so an Owner's click at the same moment sends it once; whoever loses leaves the row
/// as the other left it.
/// </para>
/// <para>
/// <b>The switches hold</b>: with all SMS or the message's kind off, nothing is resent, as the
/// settings page says nothing is sent. The Owner's resend is still allowed then. And the sending
/// hours: what is still cancelled when they end stays so, with the Owner's resend.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, never two at once.
/// </para>
/// </remarks>
public sealed partial class ResendCancelledSmsHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    CheckSmsDeliveryHandler deliveries,
    ILogger<ResendCancelledSmsHandler> logger)
{
    /// <summary>The least time between a cancelled message's last request and the next (§10).</summary>
    public static readonly TimeSpan WaitBetweenTries = TimeSpan.FromMinutes(30);

    /// <returns>How many cancelled messages were sent again and accepted by the provider.</returns>
    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        var startOfToday = calendar.StartOfDayUtc(calendar.Today());
        await deliveries.CheckSentSinceAsync(startOfToday, cancellationToken);

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled)
        {
            return 0;
        }

        var lastSentBy = time.GetUtcNow() - WaitBetweenTries;
        var cancelled = await db.Notifications.AsNoTracking()
            .Where(notification => notification.Status == NotificationStatus.Sent
                && notification.Delivery == SmsDelivery.Cancelled
                && notification.AutoResends < Notification.MaxAutoResends
                && notification.SentAt >= startOfToday
                && notification.SentAt <= lastSentBy)
            .OrderBy(notification => notification.SentAt)
            .Select(notification => new { notification.Id, notification.Kind })
            .ToListAsync(cancellationToken);
        if (cancelled.Count == 0)
        {
            return 0;
        }

        var resent = 0;
        foreach (var message in cancelled)
        {
            // Read again for each one: a request may take up to 30 seconds, and 22:00 may come between.
            if (!settings.For(message.Kind).Enabled
                || !SmsSettings.IsWithinSendingHours(message.Kind, calendar.TimeOfDay()))
            {
                continue;
            }

            var outcome = await ResendAsync(message.Id, cancellationToken);
            if (outcome == SmsSendOutcome.Sent)
            {
                resent++;
            }
            else if (outcome == SmsSendOutcome.CreditExhausted)
            {
                // The rest would meet the same empty account; they stay cancelled for the Owner.
                break;
            }
        }

        LogResent(logger, cancelled.Count, resent);

        return resent;
    }

    /// <returns>How its request went; <c>null</c> when it was not made (someone else changed the message first).</returns>
    private async Task<SmsSendOutcome?> ResendAsync(Guid id, CancellationToken cancellationToken)
    {
        // One message at a time in a clean context, so a conflict on one leaves nothing for the next save.
        db.ChangeTracker.Clear();
        var notification = await db.Notifications.SingleAsync(notification => notification.Id == id, cancellationToken);
        if (notification.PrepareAutoResend().IsFailure)
        {
            return null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The Owner resent it, or the delivery check changed it, a moment ago.
            return null;
        }

        // Not the job's token: once the SMS may have left, its outcome is recorded whatever happens.
        var result = await sender.SendAsync(new SmsMessage(notification.Recipient, notification.Text), CancellationToken.None);
        SmsResendOutcome.Record(notification, result, time.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A run of the kind starting this very moment found the row Pending and marked it
            // Unknown, which is still true, since the request may have left.
        }

        return result.Outcome;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS automatic resend: {Cancelled} cancelled by the provider today, {Resent} sent again")]
    private static partial void LogResent(ILogger logger, int cancelled, int resent);
}

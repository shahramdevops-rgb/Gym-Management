using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendSubscriptionReceipt;

/// <summary>
/// Asks for a plan's receipt when a payment makes it fully paid (BUSINESS_RULES.md §10 <i>The
/// subscription receipt</i>, task 10.9): at once within 08:00–22:00, otherwise at 10:00 the next
/// morning. Called by the handlers that take money for a subscription, after their transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>After the commit, never inside it.</b> The job must find the payment when it runs, and a
/// payment rolled back must not leave a receipt behind.
/// </para>
/// <para>
/// <b>A receipt never fails a payment.</b> The money is already saved when this runs. If the job
/// runner cannot take the receipt, that one SMS is lost and logged: the desk is answered as the
/// payment deserves (§10: these messages are information, not something the gym must be sure arrived).
/// </para>
/// </remarks>
public sealed partial class SubscriptionReceipts(
    IAppDbContext db,
    IGymCalendar calendar,
    ISubscriptionReceiptScheduler scheduler,
    ILogger<SubscriptionReceipts> logger)
{
    /// <param name="owedBefore">What was still owed on the subscription just before this payment.</param>
    /// <param name="amount">This payment.</param>
    public async Task QueueIfEarnedAsync(Guid subscriptionId, decimal owedBefore, decimal amount, CancellationToken cancellationToken)
    {
        // Cheap checks first: most payments are a part, and most of the rest find the kind off.
        if (amount < owedBefore)
        {
            return;
        }

        // §10: only while it is on when the payment is written; turning it on later reaches no one.
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.SubscriptionReceiptEnabled)
        {
            return;
        }

        var subscription = await db.Subscriptions.AsNoTracking()
            .SingleAsync(subscription => subscription.Id == subscriptionId, cancellationToken);
        if (!SmsAudience.EarnsReceipt(subscription, owedBefore, amount))
        {
            return;
        }

        try
        {
            Queue(subscriptionId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotQueued(logger, exception, subscriptionId);
        }
    }

    /// <summary>
    /// Now, or at 10:00 when it is outside the sending hours. The receipt job calls it too, for a
    /// receipt whose time came while the server was down and is now outside the hours.
    /// </summary>
    public void Queue(Guid subscriptionId)
    {
        if (SmsSettings.ReceiptDeferredUntil(calendar.Today(), calendar.TimeOfDay()) is { } morning)
        {
            var at = TimeZoneInfo.ConvertTimeToUtc(morning, calendar.TimeZone);
            scheduler.SendAt(subscriptionId, new DateTimeOffset(at, TimeSpan.Zero));
        }
        else
        {
            scheduler.SendNow(subscriptionId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The receipt of subscription {SubscriptionId} could not be queued; it will not be sent")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid subscriptionId);
}

using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.CheckSmsDelivery;

/// <summary>
/// The nightly delivery check (BUSINESS_RULES.md §10 <i>Sending</i>): asks the provider whether each
/// message sent in the last 48 hours reached the phone, and keeps the answer on the message. A blocked
/// or cancelled message's cost becomes 0: the provider gives it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Once a day is enough.</b> The provider remembers a message for 48 hours, so a check every 24
/// asks about each one at least once, and a "not delivered" (the phone was off) is asked about again
/// the next night, when it may have arrived. Delivered, blocked and cancelled do not change, so they
/// are left. Today's messages are also asked about every 15 minutes, by the automatic resend of what
/// the provider cancelled (<see cref="CheckSentSinceAsync"/>, task 10.16).
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs schedules it directly with Hangfire. A
/// provider that does not answer leaves every message as it was: <see cref="ISmsAccount"/> logs it.
/// </para>
/// </remarks>
public sealed partial class CheckSmsDeliveryHandler(
    IAppDbContext db,
    TimeProvider time,
    ISmsAccount account,
    ILogger<CheckSmsDeliveryHandler> logger)
{
    /// <summary>How long the provider keeps a message's delivery (§10).</summary>
    public static readonly TimeSpan ProviderMemory = TimeSpan.FromHours(48);

    /// <returns>How many messages got a new delivery.</returns>
    public Task<int> Handle(CancellationToken cancellationToken) =>
        CheckSentSinceAsync(time.GetUtcNow() - ProviderMemory, cancellationToken);

    /// <summary>
    /// The same check for the messages sent from <paramref name="since"/> on: the automatic resend
    /// asks about today's every 15 minutes, to find what the provider cancelled (task 10.16).
    /// </summary>
    /// <returns>How many messages got a new delivery.</returns>
    public async Task<int> CheckSentSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var waiting = await db.Notifications
            .Where(notification => notification.Status == NotificationStatus.Sent
                && notification.SentAt > since
                && notification.ProviderMessageId != null
                && (notification.Delivery == null || notification.Delivery == SmsDelivery.NotDelivered))
            .ToListAsync(cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        var ids = waiting.Select(notification => notification.ProviderMessageId!.Value).Distinct().ToList();
        var answers = await account.GetDeliveriesAsync(ids, cancellationToken);

        var changed = 0;
        foreach (var notification in waiting)
        {
            if (answers.TryGetValue(notification.ProviderMessageId!.Value, out var delivery)
                && notification.Delivery != delivery
                && notification.RecordDelivery(delivery).IsSuccess)
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The nightly check and the automatic resend's may meet at 23:30, or a resend changed
                // a message meanwhile. Nothing is lost: the provider is asked again next time.
                db.ChangeTracker.Clear();
                LogConflict(logger);
                return 0;
            }
        }

        LogChecked(logger, waiting.Count, changed);

        return changed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS delivery check: asked about {Asked}, {Changed} with a new delivery")]
    private static partial void LogChecked(ILogger logger, int asked, int changed);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS delivery check: a message changed meanwhile; asked again next time")]
    private static partial void LogConflict(ILogger logger);
}

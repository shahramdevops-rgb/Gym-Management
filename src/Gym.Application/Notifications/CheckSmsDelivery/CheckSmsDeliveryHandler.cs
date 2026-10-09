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
/// are left.
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
    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        var since = time.GetUtcNow() - ProviderMemory;

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
            await db.SaveChangesAsync(cancellationToken);
        }

        LogChecked(logger, waiting.Count, changed);

        return changed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS delivery check: asked about {Asked}, {Changed} with a new delivery")]
    private static partial void LogChecked(ILogger logger, int asked, int changed);
}

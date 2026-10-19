using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications;

/// <summary>
/// One request for a message already saved <c>Pending</c>, and its outcome saved on the row
/// (BUSINESS_RULES.md §10 <i>The daily runs</i>). Shared by the daily runs and the receipt, so both
/// record an outcome the same way.
/// </summary>
public static class SmsRequest
{
    public static async Task<SmsSendOutcome> SendAsync(
        IAppDbContext db, ISmsSender sender, TimeProvider time, Notification notification)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(notification);

        var message = new SmsMessage(notification.Recipient, notification.Text);

        // Not the caller's token: once the request may have left, its outcome is recorded whatever happens.
        var result = await sender.SendAsync(message, CancellationToken.None);
        var now = time.GetUtcNow();

        switch (result.Outcome)
        {
            case SmsSendOutcome.Sent:
                notification.MarkSent(
                    result.ProviderMessageId ?? throw new InvalidOperationException("A sent message has no provider id."),
                    result.CostRial ?? 0m,
                    now);
                break;

            case SmsSendOutcome.RetryableFailure:
                notification.RecordRetryableFailure(result.ErrorCode, now);
                break;

            case SmsSendOutcome.PermanentFailure:
            case SmsSendOutcome.CreditExhausted:
                notification.MarkFailed(result.ErrorCode, now);
                break;

            case SmsSendOutcome.Unknown:
                notification.MarkUnknown(now);
                break;

            default:
                throw new InvalidOperationException($"Unknown send outcome {result.Outcome}.");
        }

        await db.SaveChangesAsync(CancellationToken.None);

        return result.Outcome;
    }
}

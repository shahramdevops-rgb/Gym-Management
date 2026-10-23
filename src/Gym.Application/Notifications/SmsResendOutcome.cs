using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications;

/// <summary>
/// How a resend's one request went, kept on the message (BUSINESS_RULES.md §10 <i>Sending</i>).
/// Shared by the Owner's resend (task 10.5) and the automatic one (task 10.16), so both treat every
/// answer the same way.
/// </summary>
public static class SmsResendOutcome
{
    /// <summary>
    /// One request, so no failure waits for another try: whatever may pass is <c>Failed</c> with its
    /// code, and the Owner can resend again (§10, decided with the developer, 1405/07/16).
    /// </summary>
    public static void Record(Notification notification, SmsSendResult result, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(result);

        switch (result.Outcome)
        {
            case SmsSendOutcome.Sent:
                notification.MarkSent(
                    result.ProviderMessageId ?? throw new InvalidOperationException("A sent message has no provider id."),
                    result.CostRial ?? 0m,
                    now);
                break;

            case SmsSendOutcome.RetryableFailure:
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
    }
}

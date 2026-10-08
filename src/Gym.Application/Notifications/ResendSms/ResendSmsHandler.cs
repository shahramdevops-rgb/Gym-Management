using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.ResendSms;

/// <summary>
/// The Owner resends a <c>Failed</c> or <c>Unknown</c> SMS by hand (BUSINESS_RULES.md §10
/// <i>Sending</i>, task 10.5): exactly the same message, one request, no retries. Owner only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Saved before the request, like a run.</b> The row goes back to <c>Pending</c> and is saved, then
/// the request is made, then its outcome is saved. Two resends of one message at once cannot both
/// pass the first save (<c>xmin</c>), so the provider gets one request and the second Owner click a
/// conflict. If the server stops in between, the kind's next run marks the row <c>Unknown</c>.
/// </para>
/// <para>
/// <b>Allowed while the switches are off.</b> They stop the daily runs; a resend is the Owner's own
/// choice. The sending hours still hold.
/// </para>
/// </remarks>
public sealed class ResendSmsHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time, ISmsSender sender)
{
    public async Task<Result<SmsMessageResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notification is null)
        {
            return Result.Failure<SmsMessageResponse>(NotificationErrors.NotFound);
        }

        // What is wrong with the message comes before "not now": resending it later would not help.
        var prepared = notification.PrepareResend();
        if (prepared.IsFailure)
        {
            return Result.Failure<SmsMessageResponse>(prepared.Error);
        }

        // Nothing is saved on this path, so the change above goes with the request.
        if (!SmsSettings.IsWithinSendingHours(calendar.TimeOfDay()))
        {
            return Result.Failure<SmsMessageResponse>(NotificationErrors.OutsideSendingHours);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SmsMessageResponse>(NotificationErrors.ChangedConcurrently);
        }

        // Not the request's token: once the SMS may have left, its outcome is recorded whatever happens.
        var message = new SmsTemplateMessage(notification.Recipient, notification.TemplateName, notification.Tokens);
        var result = await sender.SendTemplateAsync(message, CancellationToken.None);
        Record(notification, result, time.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Only a run starting this very moment can get here: it found the row Pending and marked
            // it Unknown, which is still true, since the request may have left.
            return Result.Failure<SmsMessageResponse>(NotificationErrors.ChangedConcurrently);
        }

        var memberName = notification.MemberId is { } memberId
            ? await db.Members.AsNoTracking()
                .Where(member => member.Id == memberId)
                .Select(member => member.FullName)
                .SingleOrDefaultAsync(CancellationToken.None)
            : null;

        return SmsMessageResponse.From(notification, memberName);
    }

    /// <summary>
    /// One request, so no failure waits for another try: whatever may pass is <c>Failed</c> with its
    /// code, and the Owner can resend again (§10, decided with the developer, 1405/07/16).
    /// </summary>
    private static void Record(Notification notification, SmsSendResult result, DateTimeOffset now)
    {
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

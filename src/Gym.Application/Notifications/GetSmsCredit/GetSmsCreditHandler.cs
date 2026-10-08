using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.GetSmsCredit;

/// <summary>
/// The SMS account's remaining credit, and whether the messages say it ran out, for both SMS pages
/// (BUSINESS_RULES.md §10 <i>Sending</i>). Owner only. Asks the provider each time: it costs nothing,
/// and the pages are opened rarely.
/// </summary>
public sealed class GetSmsCreditHandler(IAppDbContext db, ISmsAccount account)
{
    public async Task<SmsCreditResponse> Handle(CancellationToken cancellationToken)
    {
        var credit = await account.GetCreditAsync(cancellationToken);

        return SmsCreditResponse.From(credit, await IsCreditUsedUpAsync(cancellationToken));
    }

    /// <summary>
    /// The latest failure for credit against the latest message sent: once anything is sent again, by
    /// a run or a resend, the account has credit and the warning goes away by itself.
    /// </summary>
    private async Task<bool> IsCreditUsedUpAsync(CancellationToken cancellationToken)
    {
        var lastCreditFailure = await db.Notifications.AsNoTracking()
            .Where(notification => notification.Status == NotificationStatus.Failed
                && notification.ErrorCode == SmsProviderCodes.CreditUsedUp)
            .MaxAsync(notification => notification.LastAttemptAt, cancellationToken);
        if (lastCreditFailure is not { } failedAt)
        {
            return false;
        }

        var lastSent = await db.Notifications.AsNoTracking()
            .Where(notification => notification.Status == NotificationStatus.Sent)
            .MaxAsync(notification => notification.SentAt, cancellationToken);

        return lastSent is not { } sentAt || sentAt < failedAt;
    }
}

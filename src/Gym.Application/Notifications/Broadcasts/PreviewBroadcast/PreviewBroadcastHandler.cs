using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.Broadcasts.PreviewBroadcast;

/// <summary>
/// <c>POST /api/sms/broadcasts/preview</c>: the figures the confirmation shows before a broadcast is
/// sent (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13). Writes nothing. Owner only.
/// </summary>
/// <remarks>
/// It refuses only what is wrong with the text (empty, too long). The switches and the hours are the
/// send's to check: the Owner may write a text at night and send it in the morning.
/// </remarks>
public sealed class PreviewBroadcastHandler(IAppDbContext db, IGymCalendar calendar, BroadcastRecipients recipients)
{
    public async Task<Result<BroadcastPreviewResponse>> Handle(PreviewBroadcastCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = SmsBroadcast.Create(command.Text ?? string.Empty, command.Audience);
        if (created.IsFailure)
        {
            return Result.Failure<BroadcastPreviewResponse>(created.Error);
        }

        var text = created.Value.Text;
        var group = await recipients.FindAsync(command.Audience, calendar.Today(), withoutMessageOf: null, cancellationToken);
        var longest = BroadcastRecipients.LongestFirstName(group);
        if (SmsBroadcast.CheckLength(text, longest) is { } tooLong)
        {
            return Result.Failure<BroadcastPreviewResponse>(tooLong);
        }

        var totalParts = group.Sum(recipient => SmsParts.Count(SmsText.ForBroadcast(recipient.FullName, text)));
        var sample = SmsText.ForBroadcast(longest, text);
        var pricePerPartRial = await LatestPricePerPartRialAsync(cancellationToken);

        return new BroadcastPreviewResponse(
            group.Count,
            group.Count == 0 ? 0 : SmsParts.Count(sample),
            totalParts,
            pricePerPartRial is { } price ? decimal.Round(price * totalParts / 10m, 0, MidpointRounding.AwayFromZero) : null,
            sample);
    }

    /// <summary>
    /// The latest charged message's cost divided by its parts. A blocked or cancelled one cost 0 (the
    /// provider gave it back), so it tells no price and is skipped.
    /// </summary>
    private async Task<decimal?> LatestPricePerPartRialAsync(CancellationToken cancellationToken)
    {
        var latest = await db.Notifications.AsNoTracking()
            .Where(notification => notification.Status == NotificationStatus.Sent && notification.CostRial > 0m)
            .OrderByDescending(notification => notification.SentAt)
            .ThenByDescending(notification => notification.Id)
            .Select(notification => new { notification.Text, notification.CostRial })
            .FirstOrDefaultAsync(cancellationToken);

        return latest?.CostRial is { } cost ? cost / SmsParts.Count(latest.Text) : null;
    }
}

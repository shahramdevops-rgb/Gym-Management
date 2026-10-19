using Gym.Application.Common.Paging;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.ListSmsMessages;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/sms/messages?from=2026-09-23&amp;to=2026-10-22&amp;kind=Birthday&amp;status=Failed&amp;page=1</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with the day the message was written in the gym's time zone. <c>null</c>
/// means no lower bound. The page sends the current Jalali month when the Owner has chosen nothing.
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="Kind">One kind only; omitted means every kind.</param>
/// <param name="Status">One status only; omitted means every status.</param>
/// <param name="BroadcastId">One broadcast's messages only (task 10.13), opened from «پیامک گروهی».</param>
public sealed record ListSmsMessagesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    NotificationKind? Kind = null,
    NotificationStatus? Status = null,
    Guid? BroadcastId = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);

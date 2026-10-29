using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.ListSmsMessages;

/// <summary>
/// The SMS history (پیامک‌ها), the latest first, with the cost of what the filter matches
/// (BUSINESS_RULES.md §10 <i>The SMS history</i>, task 10.5). Owner only.
/// </summary>
public sealed class ListSmsMessagesHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<SmsMessageListResponse> Handle(ListSmsMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var messages = db.Notifications.AsNoTracking();

        // A message's date is the day it was written, in the gym's time zone: the days become a
        // range of moments, the end being the midnight after the last day.
        if (query.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            messages = messages.Where(notification => notification.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            messages = messages.Where(notification => notification.CreatedAt < end);
        }

        if (query.Kind is { } kind)
        {
            messages = messages.Where(notification => notification.Kind == kind);
        }

        if (query.Status is { } status)
        {
            messages = messages.Where(notification => notification.Status == status);
        }

        if (query.BroadcastId is { } broadcastId)
        {
            messages = messages.Where(notification => notification.BroadcastId == broadcastId);
        }

        var totalCount = await messages.CountAsync(cancellationToken);

        // Only a sent message has a cost; the others add nothing.
        var totalCostRial = await messages.SumAsync(notification => notification.CostRial ?? 0m, cancellationToken);

        // Id breaks the last tie so paging never repeats or skips a row.
        var rows = await messages
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var names = await SmsRecipientNames.ForAsync(db, rows, cancellationToken);
        var items = rows
            .Select(notification => SmsMessageResponse.From(notification, names.GetValueOrDefault(notification.Id)))
            .ToList();

        return new SmsMessageListResponse(items, query.Page, query.PageSize, totalCount, totalCostRial / 10m);
    }
}

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

        var names = await MemberNamesAsync(rows, cancellationToken);
        var items = rows
            .Select(notification => SmsMessageResponse.From(notification, NameOf(names, notification)))
            .ToList();

        return new SmsMessageListResponse(items, query.Page, query.PageSize, totalCount, totalCostRial / 10m);
    }

    /// <summary>The names of the members on one page, in one query.</summary>
    private async Task<Dictionary<Guid, string>> MemberNamesAsync(
        IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        var memberIds = notifications
            .Where(notification => notification.MemberId is not null)
            .Select(notification => notification.MemberId!.Value)
            .Distinct()
            .ToList();
        if (memberIds.Count == 0)
        {
            return [];
        }

        return await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);
    }

    private static string? NameOf(Dictionary<Guid, string> names, Notification notification) =>
        notification.MemberId is { } memberId ? names.GetValueOrDefault(memberId) : null;
}

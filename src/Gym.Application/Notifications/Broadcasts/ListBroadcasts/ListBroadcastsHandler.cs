using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.Broadcasts.ListBroadcasts;

/// <summary>
/// The broadcasts on «پیامک گروهی», the latest first, each with how its messages went
/// (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13). Owner only.
/// </summary>
public sealed class ListBroadcastsHandler(IAppDbContext db)
{
    public async Task<PagedResponse<BroadcastResponse>> Handle(ListBroadcastsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var broadcasts = db.SmsBroadcasts.AsNoTracking();
        var totalCount = await broadcasts.CountAsync(cancellationToken);

        // Id breaks the last tie so paging never repeats or skips a row.
        var rows = await broadcasts
            .OrderByDescending(broadcast => broadcast.CreatedAt)
            .ThenByDescending(broadcast => broadcast.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var counts = await BroadcastCounts.ForAsync(db, [.. rows.Select(broadcast => broadcast.Id)], cancellationToken);
        var items = rows
            .Select(broadcast => BroadcastResponse.From(
                broadcast, [.. counts.Where(count => count.BroadcastId == broadcast.Id)]))
            .ToList();

        return new PagedResponse<BroadcastResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

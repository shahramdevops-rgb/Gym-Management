using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.Broadcasts;

/// <summary>How the messages of some broadcasts went, read from the messages themselves, in one query.</summary>
public static class BroadcastCounts
{
    public static async Task<List<BroadcastMessageCount>> ForAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> broadcastIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(broadcastIds);

        if (broadcastIds.Count == 0)
        {
            return [];
        }

        return await db.Notifications.AsNoTracking()
            .Where(notification => notification.BroadcastId != null && broadcastIds.Contains(notification.BroadcastId.Value))
            .GroupBy(notification => new { BroadcastId = notification.BroadcastId!.Value, notification.Status })
            .Select(group => new BroadcastMessageCount(
                group.Key.BroadcastId,
                group.Key.Status,
                group.Count(),
                group.Sum(notification => notification.CostRial ?? 0m)))
            .ToListAsync(cancellationToken);
    }
}

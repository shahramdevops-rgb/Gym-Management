using Gym.Application.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables;

/// <summary>Reading a trainer's patterns for <see cref="TimetableRules.SessionsOn"/>.</summary>
internal static class TrainerTimetable
{
    /// <summary>
    /// The patterns that reach <paramref name="from"/> or a later date: the one in force on it, and
    /// every one that starts after it. The older ones only say what the past was, and stay in the
    /// database for that (15.7), unread here.
    /// </summary>
    public static async Task<List<WeekPattern>> PatternsFromAsync(IAppDbContext db, Guid trainerId, DateOnly from, CancellationToken cancellationToken)
    {
        var inForceSince = await db.WeekPatterns
            .Where(pattern => pattern.TrainerId == trainerId && pattern.StartsOn <= from)
            .MaxAsync(pattern => (DateOnly?)pattern.StartsOn, cancellationToken);
        var earliest = inForceSince ?? from;

        return await db.WeekPatterns.AsNoTracking()
            .Include(pattern => pattern.Sessions)
            .Where(pattern => pattern.TrainerId == trainerId && pattern.StartsOn >= earliest)
            .ToListAsync(cancellationToken);
    }
}

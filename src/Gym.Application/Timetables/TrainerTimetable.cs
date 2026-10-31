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

    /// <summary>The dates from <paramref name="from"/> to <paramref name="to"/> the trainer changed on their own.</summary>
    public static Task<Dictionary<DateOnly, TimetableDay>> ChangedDaysAsync(
        IAppDbContext db, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        db.TimetableDays.AsNoTracking()
            .Include(day => day.Sessions)
            .Where(day => day.TrainerId == trainerId && day.Date >= from && day.Date <= to)
            .ToDictionaryAsync(day => day.Date, cancellationToken);

    /// <summary>Each date's sessions from <paramref name="from"/> to <paramref name="to"/>, as <see cref="TimetableRules.SessionsOn"/> works them out.</summary>
    public static async Task<TimetableDates> DatesAsync(IAppDbContext db, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var patterns = await PatternsFromAsync(db, trainerId, from, cancellationToken);
        var changed = await ChangedDaysAsync(db, trainerId, from, to, cancellationToken);

        return new TimetableDates(patterns, changed);
    }
}

/// <summary>A trainer's timetable over some dates, read once: a date's sessions are then worked out in memory.</summary>
internal sealed class TimetableDates(IReadOnlyList<WeekPattern> patterns, IReadOnlyDictionary<DateOnly, TimetableDay> changed)
{
    public IReadOnlyList<WeekPattern> Patterns => patterns;

    public IReadOnlyDictionary<DateOnly, TimetableDay> Changed => changed;

    /// <summary>The date's sessions, earliest first; the date must be in the range read.</summary>
    public IReadOnlyList<PlannedSession> SessionsOn(DateOnly date) =>
        TimetableRules.SessionsOn(date, patterns, changed.GetValueOrDefault(date));
}

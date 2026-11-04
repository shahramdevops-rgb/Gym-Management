using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>
/// A trainer's weekly pattern from <see cref="StartsOn"/> on (BUSINESS_RULES.md §15 <i>The
/// trainer's sessions</i>): each day of the week has its own sessions, repeated every week until a
/// later pattern starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>A change is a new pattern from a date</b>, tomorrow at the earliest, so the days before it
/// keep the sessions they had and the attendance on them (15.7) stays true. The patterns that
/// started earlier are kept for those days. Saving one from a date replaces any the trainer had
/// saved from that date or a later one; that is the handler's, since those are other rows.
/// </para>
/// <para>
/// One per trainer and start date, held by a unique index too.
/// </para>
/// </remarks>
public sealed class WeekPattern : Entity
{
    private readonly List<WeekPatternSession> _sessions = [];

    // For EF Core.
    private WeekPattern()
    {
    }

    public Guid TrainerId { get; private set; }

    /// <summary>The first day it is in force.</summary>
    public DateOnly StartsOn { get; private set; }

    public IReadOnlyList<WeekPatternSession> Sessions => _sessions;

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <param name="today">The gym's today: the pattern starts after it, within <see cref="TimetableRules.LastDay"/>.</param>
    public static Result<WeekPattern> Create(Guid trainerId, DateOnly startsOn, IReadOnlyCollection<WeeklySession> sessions, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        if (startsOn <= today || startsOn > TimetableRules.LastDay(today))
        {
            return Result.Failure<WeekPattern>(TimetableErrors.PatternStartOutOfRange);
        }

        var checkedWeek = TimetableRules.CheckWeek(sessions);
        if (checkedWeek.IsFailure)
        {
            return Result.Failure<WeekPattern>(checkedWeek.Error);
        }

        var pattern = new WeekPattern { TrainerId = trainerId, StartsOn = startsOn };
        pattern._sessions.AddRange(sessions.Select(session => WeekPatternSession.For(pattern.Id, session)));

        return pattern;
    }

    /// <summary>The sessions of every <paramref name="day"/> while this pattern is in force.</summary>
    public IEnumerable<PlannedSession> SessionsOn(DayOfWeek day) =>
        _sessions.Where(session => session.Day == day).Select(session => session.ToPlanned());
}

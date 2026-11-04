using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>
/// The trainer's timetable (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>, task 15.6a): what a
/// day's sessions may be, which dates can change, and what a date's sessions are.
/// </summary>
/// <remarks>
/// <para>
/// <b>Worked out, not stored.</b> Only what the trainer did is saved: the weekly patterns
/// (<see cref="WeekPattern"/>) and the dates they changed on their own (<see cref="TimetableDay"/>).
/// A date's sessions come from <see cref="SessionsOn"/> each time they are read, so no rows are
/// made ahead for twelve weeks, nothing has to fill a new day every night, and a new pattern is one
/// save rather than one for every date it reaches.
/// </para>
/// </remarks>
public static class TimetableRules
{
    public const int MinCapacity = 1;

    public const int MaxCapacity = 50;

    /// <summary>Times are on the quarter hour, :00, :15, :30 and :45 (the developer, 1405/08/13; 5 minutes before).</summary>
    public const int MinuteStep = 15;

    public const int MaxSessionsPerDay = 20;

    /// <summary>No session starts before 07:00 (the developer, 1405/08/13).</summary>
    public static readonly TimeOnly EarliestStart = new(7, 0);

    /// <summary>No session ends after 22:00 (the developer, 1405/08/13).</summary>
    public static readonly TimeOnly LatestEnd = new(22, 0);

    /// <summary>
    /// How far the timetable reaches: today and the 83 days after it, 12 weeks, so a program of 6 to 8
    /// weeks fits in it whole (accepted by the developer, 1405/08/09).
    /// </summary>
    public const int DaysAhead = 84;

    /// <summary>
    /// How many of those days the trainer's «روزهای پیش رو» shows: today and the 27 after it, 4 weeks
    /// (the developer, 1405/08/13). The timetable still reaches <see cref="DaysAhead"/> days.
    /// </summary>
    public const int DaysShown = 28;

    /// <summary>The timetable's last day, seen from <paramref name="today"/>.</summary>
    public static DateOnly LastDay(DateOnly today) => today.AddDays(DaysAhead - 1);

    /// <summary>One day's sessions: each on its own, then the day as a whole.</summary>
    public static Result CheckDay(IReadOnlyCollection<PlannedSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        if (sessions.Count > MaxSessionsPerDay)
        {
            return Result.Failure(TimetableErrors.TooManySessions);
        }

        foreach (var session in sessions)
        {
            if (session.Capacity is < MinCapacity or > MaxCapacity)
            {
                return Result.Failure(TimetableErrors.CapacityOutOfRange);
            }

            if (!IsOnStep(session.Start) || !IsOnStep(session.End))
            {
                return Result.Failure(TimetableErrors.TimeNotOnStep);
            }

            // TimeOnly has no 24:00, so a session that would end after midnight ends "before" it starts.
            if (session.End <= session.Start)
            {
                return Result.Failure(TimetableErrors.EndNotAfterStart);
            }

            if (session.Start < EarliestStart || session.End > LatestEnd)
            {
                return Result.Failure(TimetableErrors.OutsideHours);
            }
        }

        // Overlaps are allowed (the developer, 1405/08/09); the same start and end twice is not.
        var distinct = sessions.Select(session => (session.Start, session.End)).Distinct().Count();

        return distinct == sessions.Count ? Result.Success() : Result.Failure(TimetableErrors.DuplicateSession);
    }

    /// <summary>A week's sessions: each day checked as <see cref="CheckDay"/>.</summary>
    public static Result CheckWeek(IReadOnlyCollection<WeeklySession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        if (sessions.Any(session => !Enum.IsDefined(session.Day)))
        {
            return Result.Failure(TimetableErrors.InvalidDay);
        }

        foreach (var day in sessions.GroupBy(session => session.Day))
        {
            var checkedDay = CheckDay(day.Select(session => session.ToPlanned()).ToList());
            if (checkedDay.IsFailure)
            {
                return checkedDay;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether <paramref name="date"/>'s sessions may become <paramref name="next"/>: a day from today
    /// to <see cref="LastDay"/>, and on today, every session already started stays as it was and no
    /// new one starts in the past.
    /// </summary>
    /// <param name="current">The date's sessions now, from <see cref="SessionsOn"/>.</param>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result CheckChange(
        DateOnly date,
        IReadOnlyCollection<PlannedSession> current,
        IReadOnlyCollection<PlannedSession> next,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        if (date < today || date > LastDay(today))
        {
            return Result.Failure(TimetableErrors.DayOutOfRange);
        }

        var checkedDay = CheckDay(next);
        if (checkedDay.IsFailure || date != today)
        {
            return checkedDay;
        }

        // A session whose start has come counts as started, as a check-in at its start would.
        var started = current.Where(session => session.Start <= timeOfDay).ToList();
        if (started.Any(session => !next.Contains(session)))
        {
            return Result.Failure(TimetableErrors.SessionStarted);
        }

        if (next.Any(session => session.Start <= timeOfDay && !started.Contains(session)))
        {
            return Result.Failure(TimetableErrors.SessionInThePast);
        }

        return Result.Success();
    }

    /// <summary>
    /// <paramref name="date"/>'s sessions, earliest first: the trainer's change for that date when
    /// there is one, else the day of the week in the pattern in force on it, the one that started
    /// last on or before it. Before the trainer's first pattern, a date has none.
    /// </summary>
    /// <param name="patterns">The trainer's patterns, in any order.</param>
    /// <param name="changedDay">The trainer's change for <paramref name="date"/>, if any.</param>
    public static IReadOnlyList<PlannedSession> SessionsOn(DateOnly date, IEnumerable<WeekPattern> patterns, TimetableDay? changedDay)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        if (changedDay is not null)
        {
            if (changedDay.Date != date)
            {
                throw new ArgumentException("The change is for another date.", nameof(changedDay));
            }

            return Ordered(changedDay.Sessions.Select(session => session.ToPlanned()));
        }

        var inForce = patterns.Where(pattern => pattern.StartsOn <= date).MaxBy(pattern => pattern.StartsOn);

        return inForce is null ? [] : Ordered(inForce.SessionsOn(date.DayOfWeek));
    }

    private static List<PlannedSession> Ordered(IEnumerable<PlannedSession> sessions) =>
        sessions.OrderBy(session => session.Start).ThenBy(session => session.End).ToList();

    private static bool IsOnStep(TimeOnly time) =>
        time.Ticks % TimeSpan.FromMinutes(MinuteStep).Ticks == 0;
}

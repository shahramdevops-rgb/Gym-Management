using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>
/// One date the trainer changed on its own (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>):
/// its sessions are these, whatever the pattern says. No sessions means the day is closed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kept through later patterns.</b> A pattern saved afterwards does not reach this date; the
/// trainer's «برگرداندن به الگو» does, by deleting this row (<see cref="CheckReset"/> first). A
/// timetable is not a financial record, so the row really goes.
/// </para>
/// <para>
/// One per trainer and date, held by a unique index too.
/// </para>
/// </remarks>
public sealed class TimetableDay : Entity
{
    private readonly List<TimetableDaySession> _sessions = [];

    // For EF Core.
    private TimetableDay()
    {
    }

    public Guid TrainerId { get; private set; }

    public DateOnly Date { get; private set; }

    public IReadOnlyList<TimetableDaySession> Sessions => _sessions;

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    public bool IsClosed => _sessions.Count == 0;

    /// <summary>The trainer's first change of <paramref name="date"/>.</summary>
    /// <param name="current">The date's sessions now, from the pattern.</param>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<TimetableDay> Create(
        Guid trainerId,
        DateOnly date,
        IReadOnlyCollection<PlannedSession> current,
        IReadOnlyCollection<PlannedSession> sessions,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        var allowed = TimetableRules.CheckChange(date, current, sessions, today, timeOfDay);
        if (allowed.IsFailure)
        {
            return Result.Failure<TimetableDay>(allowed.Error);
        }

        var day = new TimetableDay { TrainerId = trainerId, Date = date };
        day.Replace(sessions);

        return day;
    }

    /// <summary>The trainer changes the date again: its sessions become <paramref name="sessions"/>.</summary>
    public Result Change(IReadOnlyCollection<PlannedSession> sessions, DateOnly today, TimeOnly timeOfDay)
    {
        var allowed = TimetableRules.CheckChange(Date, Current(), sessions, today, timeOfDay);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Replace(sessions);

        return Result.Success();
    }

    /// <summary>
    /// Whether the date may go back to the pattern's <paramref name="patternSessions"/>: the same
    /// rules as any change, so a session of today that has started stays.
    /// </summary>
    public Result CheckReset(IReadOnlyCollection<PlannedSession> patternSessions, DateOnly today, TimeOnly timeOfDay) =>
        TimetableRules.CheckChange(Date, Current(), patternSessions, today, timeOfDay);

    private List<PlannedSession> Current() => _sessions.Select(session => session.ToPlanned()).ToList();

    private void Replace(IReadOnlyCollection<PlannedSession> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions.Select(session => TimetableDaySession.For(Id, session)));
    }
}

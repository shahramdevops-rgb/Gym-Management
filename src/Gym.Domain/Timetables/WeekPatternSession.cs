using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>One session of a <see cref="WeekPattern"/>, on one day of the week.</summary>
public sealed class WeekPatternSession : Entity
{
    // For EF Core.
    private WeekPatternSession()
    {
    }

    public Guid PatternId { get; private set; }

    public DayOfWeek Day { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public int Capacity { get; private set; }

    internal static WeekPatternSession For(Guid patternId, WeeklySession session) => new()
    {
        PatternId = patternId,
        Day = session.Day,
        Start = session.Start,
        End = session.End,
        Capacity = session.Capacity,
    };

    public PlannedSession ToPlanned() => new(Start, End, Capacity);

    public WeeklySession ToWeekly() => new(Day, Start, End, Capacity);
}

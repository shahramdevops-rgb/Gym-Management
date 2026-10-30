using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

/// <summary>One session of a <see cref="TimetableDay"/>.</summary>
public sealed class TimetableDaySession : Entity
{
    // For EF Core.
    private TimetableDaySession()
    {
    }

    public Guid DayId { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public int Capacity { get; private set; }

    internal static TimetableDaySession For(Guid dayId, PlannedSession session) => new()
    {
        DayId = dayId,
        Start = session.Start,
        End = session.End,
        Capacity = session.Capacity,
    };

    public PlannedSession ToPlanned() => new(Start, End, Capacity);
}

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

    /// <summary>Marked VIP by the trainer: for now only a label (BUSINESS_RULES.md §15 <i>VIP sessions</i>).</summary>
    public bool IsVip { get; private set; }

    /// <summary>The trainer's standing note on it, «یادداشت ثابت» (BUSINESS_RULES.md §15 <i>Session notes</i>); <c>null</c> for none.</summary>
    public string? Note { get; private set; }

    internal static TimetableDaySession For(Guid dayId, PlannedSession session) => new()
    {
        DayId = dayId,
        Start = session.Start,
        End = session.End,
        Capacity = session.Capacity,
        IsVip = session.IsVip,
        Note = session.Note,
    };

    public PlannedSession ToPlanned() => new(Start, End, Capacity, IsVip, Note);
}

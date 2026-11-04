using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>One weekly session of a <see cref="FixedSchedule"/>: the trainer's session from <see cref="Start"/> to <see cref="End"/> on every <see cref="Day"/>.</summary>
public sealed class FixedScheduleSlot : Entity
{
    // For EF Core.
    private FixedScheduleSlot()
    {
    }

    public Guid ScheduleId { get; private set; }

    public DayOfWeek Day { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    internal static FixedScheduleSlot For(Guid scheduleId, WeeklySlot slot) => new()
    {
        ScheduleId = scheduleId,
        Day = slot.Day,
        Start = slot.Start,
        End = slot.End,
    };

    public WeeklySlot ToWeekly() => new(Day, Start, End);
}

/// <summary>A session of the trainer's week, named by its day and times: sessions have no other name (15.6a).</summary>
public sealed record WeeklySlot(DayOfWeek Day, TimeOnly Start, TimeOnly End);

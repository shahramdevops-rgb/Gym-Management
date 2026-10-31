using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Domain.Bookings;

/// <summary>
/// A student's «برنامهٔ ثابت» with their trainer (BUSINESS_RULES.md §15 <i>Students in sessions</i>):
/// the trainer's weekly sessions the student comes to, every week from <see cref="StartsOn"/> to
/// <see cref="EndsOn"/>, e.g. Saturday, Monday and Wednesday at 13:00 for eight weeks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Worked out, not stored, as the timetable is.</b> No row is made for each date: the dates the
/// student is in come from <see cref="BookingRules.Places"/>, less the dates taken out
/// (<see cref="SessionSkip"/>), plus the dates moved to (<see cref="SessionBooking"/>).
/// </para>
/// <para>
/// <b>Tied to the link</b> (15.4) it was made under: when the link ends, the dates after its last
/// day are gone without this row changing. One schedule per link at a time, held by an exclusion
/// constraint on the dates too.
/// </para>
/// </remarks>
public sealed class FixedSchedule : Entity
{
    private readonly List<FixedScheduleSlot> _slots = [];

    // For EF Core.
    private FixedSchedule()
    {
    }

    public Guid TrainerId { get; private set; }

    public Guid MemberId { get; private set; }

    /// <summary>The trainer and student's link it was made under.</summary>
    public Guid LinkId { get; private set; }

    public DateOnly StartsOn { get; private set; }

    /// <summary>Its last date; the link ending may stop it earlier.</summary>
    public DateOnly EndsOn { get; private set; }

    public IReadOnlyList<FixedScheduleSlot> Slots => _slots;

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <param name="today">The gym's today: the schedule starts on it or later, within <see cref="TimetableRules.LastDay"/>.</param>
    public static Result<FixedSchedule> Create(
        Guid trainerId,
        Guid memberId,
        Guid linkId,
        DateOnly startsOn,
        DateOnly endsOn,
        IReadOnlyCollection<WeeklySlot> slots,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (startsOn < today || endsOn < startsOn || endsOn > TimetableRules.LastDay(today))
        {
            return Result.Failure<FixedSchedule>(BookingErrors.ScheduleDatesOutOfRange);
        }

        var checkedSlots = BookingRules.CheckSlots(slots);
        if (checkedSlots.IsFailure)
        {
            return Result.Failure<FixedSchedule>(checkedSlots.Error);
        }

        var schedule = new FixedSchedule
        {
            TrainerId = trainerId,
            MemberId = memberId,
            LinkId = linkId,
            StartsOn = startsOn,
            EndsOn = endsOn,
        };
        schedule._slots.AddRange(slots.Select(slot => FixedScheduleSlot.For(schedule.Id, slot)));

        return schedule;
    }

    /// <summary>Whether its first date has come: one that has not is replaced or removed whole.</summary>
    public bool HasStartedBy(DateOnly today) => StartsOn <= today;

    /// <summary>
    /// The trainer changes or ends the schedule: its dates up to today stay as they were, for
    /// attendance (15.7), and none after it is left.
    /// </summary>
    public void EndAfter(DateOnly today)
    {
        if (!HasStartedBy(today))
        {
            throw new InvalidOperationException("A schedule that has not started is removed, not ended.");
        }

        if (EndsOn > today)
        {
            EndsOn = today;
        }
    }

    /// <summary>The weekly sessions it has on <paramref name="day"/>.</summary>
    public IEnumerable<FixedScheduleSlot> SlotsOn(DayOfWeek day) => _slots.Where(slot => slot.Day == day);
}

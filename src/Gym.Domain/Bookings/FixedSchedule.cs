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
    /// <param name="periodEndsOn">
    /// The last day of the link's coaching period (15.7h): the schedule ends on it or before.
    /// <c>null</c> for a link made before 15.7h that has no period yet.
    /// </param>
    public static Result<FixedSchedule> Create(
        Guid trainerId,
        Guid memberId,
        Guid linkId,
        DateOnly startsOn,
        DateOnly endsOn,
        IReadOnlyCollection<WeeklySlot> slots,
        DateOnly today,
        DateOnly? periodEndsOn = null)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (startsOn < today || endsOn < startsOn || endsOn > TimetableRules.LastDay(today))
        {
            return Result.Failure<FixedSchedule>(BookingErrors.ScheduleDatesOutOfRange);
        }

        if (endsOn > periodEndsOn)
        {
            return Result.Failure<FixedSchedule>(BookingErrors.ScheduleAfterPeriod);
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

    /// <summary>Whether it has dates before today: one that has none is replaced or removed whole.</summary>
    public bool StartedBefore(DateOnly today) => StartsOn < today;

    /// <summary>
    /// The trainer changes or ends the schedule, from now on: its dates before today stay as they
    /// were, for attendance (15.7), and none from today is left. Today's sessions that have started
    /// are kept by the caller (<see cref="BookingRules.HandOver"/>).
    /// </summary>
    public void EndBefore(DateOnly today)
    {
        if (!StartedBefore(today))
        {
            throw new InvalidOperationException("A schedule with no date before today is removed, not ended.");
        }

        var yesterday = today.AddDays(-1);
        if (EndsOn > yesterday)
        {
            EndsOn = yesterday;
        }
    }

    /// <summary>
    /// The trainer typed the days left of a link made before 15.7h, and the period ends before this
    /// schedule does: it now ends on the period's last day. One that starts after it is removed whole
    /// by the caller instead (<see cref="StartsAfter"/>).
    /// </summary>
    public void EndOnPeriodEnd(DateOnly periodEndsOn)
    {
        if (StartsAfter(periodEndsOn))
        {
            throw new InvalidOperationException("A schedule that starts after the period is removed, not ended.");
        }

        if (EndsOn > periodEndsOn)
        {
            EndsOn = periodEndsOn;
        }
    }

    /// <summary>Whether its first date is after <paramref name="day"/>.</summary>
    public bool StartsAfter(DateOnly day) => StartsOn > day;

    /// <summary>The weekly sessions it has on <paramref name="day"/>.</summary>
    public IEnumerable<FixedScheduleSlot> SlotsOn(DayOfWeek day) => _slots.Where(slot => slot.Day == day);
}

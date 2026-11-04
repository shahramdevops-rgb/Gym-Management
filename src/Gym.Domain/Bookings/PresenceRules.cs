namespace Gym.Domain.Bookings;

/// <summary>
/// Whether a student came to a session (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>,
/// task 15.7): worked out from the desk's check-ins of that day, never stored.
/// </summary>
/// <remarks>
/// The site keeps no second record of entries: a check-in is the desk's (§7), and a check-in
/// cancelled at the desk is simply not among the visits passed in, so the mark changes at once.
/// </remarks>
public static class PresenceRules
{
    /// <summary>A check-in this many minutes before the start counts for the session (proposed by Claude, accepted 1405/08/13).</summary>
    public const int EarlyMinutes = 30;

    /// <summary>A check-in up to this many minutes after the start is on time (decided by the developer, 1405/08/13, in place of 10).</summary>
    public const int OnTimeMinutes = 15;

    /// <summary>A student's attendance is read for 62 days at most: a Jalali month is 31, two fit (proposed by Claude, built with 15.7a).</summary>
    public const int MaxRangeDays = 62;

    /// <summary>
    /// The mark of a student's place in a session that is still in the timetable: on time, late,
    /// absent, or not yet (no check-in, and the session has not ended).
    /// </summary>
    /// <param name="date">The session's date.</param>
    /// <param name="start">The session's start, in the gym's time zone.</param>
    /// <param name="end">The session's end.</param>
    /// <param name="visits">The student's check-ins of that date, not cancelled, in the gym's time zone.</param>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static PresenceMark Mark(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        IEnumerable<DayVisit> visits,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(visits);

        var all = visits.ToList();
        var counted = all
            .Where(visit => CountsFor(visit, start, end))
            .OrderBy(visit => visit.CheckedInAt)
            .FirstOrDefault();

        if (counted is not null)
        {
            var onTime = Minutes(counted.CheckedInAt) <= Minutes(start) + OnTimeMinutes;

            return new PresenceMark(onTime ? SessionPresence.OnTime : SessionPresence.Late, counted.CheckedInAt, CameAfter: false);
        }

        if (!BookingRules.HasEnded(date, end, today, timeOfDay))
        {
            return new PresenceMark(SessionPresence.NotYet, CheckedInAt: null, CameAfter: false);
        }

        var after = all
            .Where(visit => Minutes(visit.CheckedInAt) >= Minutes(end))
            .OrderBy(visit => visit.CheckedInAt)
            .FirstOrDefault();

        return new PresenceMark(SessionPresence.Absent, after?.CheckedInAt, CameAfter: after is not null);
    }

    /// <summary>
    /// Whether <paramref name="visit"/> counts for the session: a check-in from
    /// <see cref="EarlyMinutes"/> before the start to the end, or an earlier one whose visit was
    /// still open at the start (the member came early and stayed).
    /// </summary>
    public static bool CountsFor(DayVisit visit, TimeOnly start, TimeOnly end)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var checkedIn = Minutes(visit.CheckedInAt);
        if (checkedIn >= Minutes(end))
        {
            return false;
        }

        if (checkedIn >= Minutes(start) - EarlyMinutes)
        {
            return true;
        }

        return visit.LeftAt is not { } left || Minutes(left) > Minutes(start);
    }

    // Minutes since midnight: a session ends the day it starts, so no time here wraps around.
    private static int Minutes(TimeOnly time) => (time.Hour * 60) + time.Minute;
}

/// <summary>A check-in of the student on the session's date, in the gym's time zone.</summary>
/// <param name="CheckedInAt">When they checked in.</param>
/// <param name="LeftAt">When they checked out that day; <c>null</c> while the visit is open, or when it closed on a later day.</param>
public sealed record DayVisit(TimeOnly CheckedInAt, TimeOnly? LeftAt);

/// <summary>A student's mark in one session.</summary>
/// <param name="CheckedInAt">The check-in that counted; for an absent student who came after the session, that later check-in.</param>
/// <param name="CameAfter">Absent, but checked in after the session ended («بعد از سانس آمد»).</param>
public sealed record PresenceMark(SessionPresence State, TimeOnly? CheckedInAt, bool CameAfter);

/// <summary>A student's state in one session (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>).</summary>
public enum SessionPresence
{
    /// <summary>No check-in yet, and the session has not ended («هنوز نیامده»).</summary>
    NotYet = 1,

    /// <summary>Checked in up to <see cref="PresenceRules.OnTimeMinutes"/> after the start, or earlier.</summary>
    OnTime = 2,

    /// <summary>Checked in later, before the session ended.</summary>
    Late = 3,

    /// <summary>The session ended with no check-in for it.</summary>
    Absent = 4,

    /// <summary>The student left that date («نمی‌آیم»), shown «مرخصی»: not absent.</summary>
    Leave = 5,

    /// <summary>The trainer removed the session, or moved its times, after the student was put in it («لغو شد»): not absent.</summary>
    Cancelled = 6,
}

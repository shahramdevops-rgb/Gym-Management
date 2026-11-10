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
    /// came after the session, absent, or not yet (no check-in, and the session has not ended).
    /// An early or late check-in carries how many minutes from the start (15.7d).
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
            // From the start, both ways (decided by the developer, 1405/08/19): 12:50 is 10 early, 13:25 is 25 late.
            var fromStart = Minutes(counted.CheckedInAt) - Minutes(start);
            if (fromStart > OnTimeMinutes)
            {
                return new PresenceMark(SessionPresence.Late, counted, MinutesLate: fromStart);
            }

            return new PresenceMark(SessionPresence.OnTime, counted, MinutesEarly: fromStart < 0 ? -fromStart : null);
        }

        if (!BookingRules.HasEnded(date, end, today, timeOfDay))
        {
            return PresenceMark.Without(SessionPresence.NotYet);
        }

        var after = all
            .Where(visit => Minutes(visit.CheckedInAt) >= Minutes(end))
            .OrderBy(visit => visit.CheckedInAt)
            .FirstOrDefault();

        // Came to the gym after the session ended: they came, so not absent (decided by the developer, 1405/08/19).
        return after is null
            ? PresenceMark.Without(SessionPresence.Absent)
            : new PresenceMark(SessionPresence.CameAfter, after);
    }

    /// <summary>
    /// The check-ins that count for none of <paramref name="sessions"/>, earliest first: the visits
    /// outside the student's sessions that day («خارج از سانس», task 15.7d). A visit after a session
    /// the student missed is one of them, as well as a visit on a date they left or whose session
    /// was cancelled.
    /// </summary>
    /// <param name="visits">The student's check-ins of one date, not cancelled.</param>
    /// <param name="sessions">The sessions the student was in that date: not left, not cancelled.</param>
    public static IReadOnlyList<DayVisit> OutsideSessions(IEnumerable<DayVisit> visits, IEnumerable<(TimeOnly Start, TimeOnly End)> sessions)
    {
        ArgumentNullException.ThrowIfNull(visits);
        ArgumentNullException.ThrowIfNull(sessions);

        var theirs = sessions.ToList();

        return visits
            .Where(visit => !theirs.Any(session => CountsFor(visit, session.Start, session.End)))
            .OrderBy(visit => visit.CheckedInAt)
            .ToList();
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
public sealed record DayVisit(TimeOnly CheckedInAt, TimeOnly? LeftAt)
{
    /// <summary>
    /// The check-out's time of day, to show beside the check-in (task 15.7d), whatever day it fell
    /// on; <c>null</c> while the visit is open, and when the nightly auto-checkout closed it.
    /// The rules above read <see cref="LeftAt"/>, never this.
    /// </summary>
    public TimeOnly? CheckedOutAt { get; init; }

    /// <summary>The nightly auto-checkout closed the visit (§7): the desk never checked the member out («خروج ثبت نشد»).</summary>
    public bool AutoClosed { get; init; }
}

/// <summary>A student's mark in one session.</summary>
/// <param name="Visit">The check-in that counted; for a student who came after the session, that later one.</param>
/// <param name="MinutesEarly">Checked in this many minutes before the start («زودتر آمد», task 15.7d); the state stays on time.</param>
/// <param name="MinutesLate">Late by this many minutes from the start (task 15.7d).</param>
public sealed record PresenceMark(SessionPresence State, DayVisit? Visit, int? MinutesEarly = null, int? MinutesLate = null)
{
    /// <summary>When <see cref="Visit"/> checked in.</summary>
    public TimeOnly? CheckedInAt => Visit?.CheckedInAt;

    /// <summary>A mark with no check-in: not yet, absent, or a place whose state comes from itself (leave, cancelled).</summary>
    public static PresenceMark Without(SessionPresence state) => new(state, Visit: null);
}

/// <summary>A student's state in one session (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>).</summary>
public enum SessionPresence
{
    /// <summary>No check-in yet, and the session has not ended («هنوز نیامده»).</summary>
    NotYet = 1,

    /// <summary>Checked in up to <see cref="PresenceRules.OnTimeMinutes"/> after the start, or earlier.</summary>
    OnTime = 2,

    /// <summary>Checked in later, before the session ended.</summary>
    Late = 3,

    /// <summary>The session ended with no check-in that day for it or after it: truly absent.</summary>
    Absent = 4,

    /// <summary>The student left that date («نمی‌آیم»), shown «مرخصی»: not absent.</summary>
    Leave = 5,

    /// <summary>The trainer removed the session, or moved its times, after the student was put in it («لغو شد»): not absent.</summary>
    Cancelled = 6,

    /// <summary>
    /// No check-in for the session, but one after it ended («بعد از سانس آمد»): they came, so not
    /// absent (decided by the developer, 1405/08/19, in place of absent with that note).
    /// </summary>
    CameAfter = 7,
}

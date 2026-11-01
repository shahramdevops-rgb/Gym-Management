using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Domain.Bookings;

/// <summary>
/// Students in the trainer's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>, task
/// 15.6b): which dates each student is in, the room a session has, and what a change of the
/// timetable does to them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A session is named by its date and times.</b> The timetable keeps no id for a date's session
/// (15.6a), so a place in one is the date, the start and the end. A change of the timetable that
/// removes the session, or moves its times, leaves the place pointing at nothing: the student sees
/// «لغو شد», and the place counts in no session.
/// </para>
/// <para>
/// <b>The capacity is a ceiling</b> (decided by the developer, 1405/08/09), but the database cannot
/// hold it: the places are worked out, not rows. The handlers count them under the trainer's lock.
/// </para>
/// </remarks>
public static class BookingRules
{
    /// <summary>A week's sessions at most: every session of every day (proposed by Claude, built with 15.6b).</summary>
    public const int MaxSlots = 7 * TimetableRules.MaxSessionsPerDay;

    /// <summary>The weekly sessions of a fixed schedule: one at least, each on the timetable's steps, none twice.</summary>
    public static Result CheckSlots(IReadOnlyCollection<WeeklySlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (slots.Count == 0)
        {
            return Result.Failure(BookingErrors.ScheduleWithoutSessions);
        }

        if (slots.Count > MaxSlots)
        {
            return Result.Failure(BookingErrors.TooManySlots);
        }

        foreach (var slot in slots)
        {
            // Times as a session's; the capacity plays no part here: it is the timetable's, read on each date.
            PlannedSession[] asSession = [new(slot.Start, slot.End, TimetableRules.MinCapacity)];
            if (!Enum.IsDefined(slot.Day) || TimetableRules.CheckDay(asSession).IsFailure)
            {
                return Result.Failure(BookingErrors.InvalidSlot);
            }
        }

        return slots.Distinct().Count() == slots.Count ? Result.Success() : Result.Failure(BookingErrors.DuplicateSlot);
    }

    /// <summary>Whether the session of <paramref name="date"/> starting at <paramref name="start"/> has started, or the date has passed.</summary>
    public static bool HasStarted(DateOnly date, TimeOnly start, DateOnly today, TimeOnly timeOfDay) =>
        date < today || (date == today && start <= timeOfDay);

    /// <summary>Whether the session of <paramref name="date"/> ending at <paramref name="end"/> is over.</summary>
    public static bool HasEnded(DateOnly date, TimeOnly end, DateOnly today, TimeOnly timeOfDay) =>
        date < today || (date == today && end <= timeOfDay);

    /// <summary>
    /// Every student's place from <paramref name="from"/> to <paramref name="to"/>, by date, then
    /// session, then member: each fixed schedule's weekly sessions on its dates, less the dates taken
    /// out, and the dates moved to. A link that has ended stops both after its last day.
    /// </summary>
    /// <param name="linkEnds">The last day of each link that has ended; an open link is not in it.</param>
    /// <remarks>A student is in a session once, however they came to be in it.</remarks>
    public static IReadOnlyList<BookedPlace> Places(
        DateOnly from,
        DateOnly to,
        IEnumerable<FixedSchedule> schedules,
        IReadOnlyDictionary<Guid, DateOnly> linkEnds,
        IEnumerable<SessionSkip> skips,
        IEnumerable<SessionBooking> bookings)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(linkEnds);
        ArgumentNullException.ThrowIfNull(skips);
        ArgumentNullException.ThrowIfNull(bookings);

        var skipped = skips.Select(skip => (skip.ScheduleId, skip.Date, skip.Start, skip.End)).ToHashSet();
        var places = new List<BookedPlace>();

        foreach (var schedule in schedules)
        {
            var last = schedule.EndsOn < to ? schedule.EndsOn : to;
            if (linkEnds.TryGetValue(schedule.LinkId, out var linkEnded) && linkEnded < last)
            {
                last = linkEnded;
            }

            for (var date = schedule.StartsOn < from ? from : schedule.StartsOn; date <= last; date = date.AddDays(1))
            {
                places.AddRange(schedule.SlotsOn(date.DayOfWeek)
                    .Where(slot => !skipped.Contains((schedule.Id, date, slot.Start, slot.End)))
                    .Select(slot => new BookedPlace(schedule.TrainerId, schedule.MemberId, date, slot.Start, slot.End, schedule.Id, BookingId: null, By: null)));
            }
        }

        places.AddRange(bookings
            .Where(booking => booking.Date >= from && booking.Date <= to)
            .Where(booking => !linkEnds.TryGetValue(booking.LinkId, out var ended) || booking.Date <= ended)
            .Select(booking => new BookedPlace(booking.TrainerId, booking.MemberId, booking.Date, booking.Start, booking.End, ScheduleId: null, booking.Id, booking.By)));

        return places
            .DistinctBy(place => (place.MemberId, place.Date, place.Start, place.End))
            .OrderBy(place => place.Date).ThenBy(place => place.Start).ThenBy(place => place.End).ThenBy(place => place.MemberId)
            .ToList();
    }

    /// <summary>The places in <paramref name="session"/> of <paramref name="date"/>.</summary>
    public static IEnumerable<BookedPlace> In(IEnumerable<BookedPlace> places, DateOnly date, PlannedSession session)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(session);

        return places.Where(place => place.Date == date && place.Start == session.Start && place.End == session.End);
    }

    /// <summary>The session of the date's <paramref name="sessions"/> that <paramref name="place"/> is in; <c>null</c> once it is gone.</summary>
    public static PlannedSession? SessionOf(BookedPlace place, IEnumerable<PlannedSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(sessions);

        return sessions.FirstOrDefault(session => session.Start == place.Start && session.End == place.End);
    }

    /// <summary>
    /// Whether <paramref name="memberId"/> may come into <paramref name="target"/> on
    /// <paramref name="date"/>, by moving a date to it or taking back their «نمی‌آیم»: a date from
    /// today to <paramref name="lastDate"/>, a session of the trainer's that has not started, that
    /// they are not in, with room.
    /// </summary>
    /// <param name="target">The session of that date with the times asked for; <c>null</c> when the date has none.</param>
    /// <param name="places">Every place of that date.</param>
    /// <param name="lastDate">The last date of the member's fixed schedule.</param>
    public static Result CheckJoin(
        Guid memberId,
        DateOnly date,
        PlannedSession? target,
        IReadOnlyCollection<BookedPlace> places,
        DateOnly lastDate,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(places);

        if (date < today || date > lastDate)
        {
            return Result.Failure(BookingErrors.MoveOutOfSchedule);
        }

        if (target is null)
        {
            return Result.Failure(BookingErrors.NoSuchSession);
        }

        if (HasStarted(date, target.Start, today, timeOfDay))
        {
            return Result.Failure(BookingErrors.SessionStarted);
        }

        var inIt = In(places, date, target).ToList();
        if (inIt.Any(place => place.MemberId == memberId))
        {
            return Result.Failure(BookingErrors.AlreadyInSession);
        }

        return inIt.Count < target.Capacity ? Result.Success() : Result.Failure(BookingErrors.SessionFull);
    }

    /// <summary>
    /// What <paramref name="schedule"/>, already among <paramref name="places"/>, puts its student in:
    /// the dates whose session is there, and those of them with no room for one more. A date with no
    /// such session is passed over (decided by the developer, 1405/08/09), and so is today's session
    /// that has started: the student cannot join it. A weekly session that is on none of its dates
    /// is not one of the trainer's.
    /// </summary>
    /// <param name="places">Every place over the schedule's dates, its own among them.</param>
    /// <param name="sessionsOn">The trainer's sessions of a date, from the timetable.</param>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<ScheduleFit> Fit(
        FixedSchedule schedule,
        IReadOnlyCollection<BookedPlace> places,
        Func<DateOnly, IReadOnlyList<PlannedSession>> sessionsOn,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(sessionsOn);

        var held = new List<BookedPlace>();
        var full = new List<BookedPlace>();
        var passedOver = new List<BookedPlace>();
        var slotsFound = new HashSet<WeeklySlot>();

        foreach (var place in places.Where(place => place.ScheduleId == schedule.Id))
        {
            var session = SessionOf(place, sessionsOn(place.Date));
            if (session is null)
            {
                passedOver.Add(place);
                continue;
            }

            // Still one of the trainer's sessions, though the student is not put in it.
            slotsFound.Add(new WeeklySlot(place.Date.DayOfWeek, place.Start, place.End));
            if (HasStarted(place.Date, place.Start, today, timeOfDay))
            {
                passedOver.Add(place);
                continue;
            }

            held.Add(place);

            var others = In(places, place.Date, session).Count(other => other.MemberId != schedule.MemberId);
            if (others >= session.Capacity)
            {
                full.Add(place);
            }
        }

        if (schedule.Slots.Any(slot => !slotsFound.Contains(slot.ToWeekly())))
        {
            return Result.Failure<ScheduleFit>(BookingErrors.NoSuchSession);
        }

        return new ScheduleFit(held, full, passedOver);
    }

    /// <summary>
    /// The places of today's sessions that have started, from the fixed schedules among
    /// <paramref name="places"/>: what a change or an end of those schedules keeps (decided by the
    /// developer, 1405/08/09: the change applies from now, and what has started stays as it was).
    /// </summary>
    public static IReadOnlyList<BookedPlace> StartedToday(IEnumerable<BookedPlace> places, DateOnly today, TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(places);

        return places
            .Where(place => place.ScheduleId is not null && place.Date == today && HasStarted(place.Date, place.Start, today, timeOfDay))
            .ToList();
    }

    /// <summary>
    /// How the student's places pass from the schedules that stop now to the new one, if any: the
    /// new one's dates it passes over are taken out of it, except today's started sessions the
    /// student was in already; those the new one does not have stay as single bookings.
    /// </summary>
    /// <param name="started">The stopped schedules' places of today that had started (<see cref="StartedToday"/>).</param>
    /// <param name="passedOver">The new schedule's <see cref="ScheduleFit.PassedOver"/>; empty when the schedule just ends.</param>
    public static ScheduleHandOver HandOver(IReadOnlyCollection<BookedPlace> started, IReadOnlyCollection<BookedPlace> passedOver)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(passedOver);

        static (DateOnly, TimeOnly, TimeOnly) Session(BookedPlace place) => (place.Date, place.Start, place.End);

        var wasIn = started.Select(Session).ToHashSet();
        var stillIn = passedOver.Select(Session).ToHashSet();

        return new ScheduleHandOver(
            PassOver: passedOver.Where(place => !wasIn.Contains(Session(place))).ToList(),
            Keep: started.Where(place => !stillIn.Contains(Session(place))).ToList());
    }

    /// <summary>
    /// What a change of the timetable does to its students (decided by the developer, 1405/08/09:
    /// the trainer is warned, then may save): each session with students that the change removes or
    /// moves, or whose new capacity is below its students.
    /// </summary>
    /// <param name="places">The places over the dates the change reaches.</param>
    /// <param name="before">A date's sessions now.</param>
    /// <param name="after">A date's sessions after the change.</param>
    public static IReadOnlyList<TouchedSession> Impact(
        IReadOnlyCollection<BookedPlace> places,
        Func<DateOnly, IReadOnlyList<PlannedSession>> before,
        Func<DateOnly, IReadOnlyList<PlannedSession>> after)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var touched = new List<TouchedSession>();
        foreach (var date in places.Select(place => place.Date).Distinct().Order())
        {
            var next = after(date);
            foreach (var session in before(date))
            {
                var students = In(places, date, session).Select(place => place.MemberId).ToList();
                if (students.Count == 0)
                {
                    continue;
                }

                var kept = next.FirstOrDefault(other => other.Start == session.Start && other.End == session.End);
                if (kept is null || kept.Capacity < students.Count)
                {
                    touched.Add(new TouchedSession(date, session.Start, session.End, students, Removed: kept is null));
                }
            }
        }

        return touched;
    }
}

/// <summary>
/// A student in a session: the date and the session's times. From their fixed schedule
/// (<see cref="ScheduleId"/>) or a single booking (<see cref="BookingId"/>, made <see cref="By"/>), one of the two.
/// </summary>
public sealed record BookedPlace(Guid TrainerId, Guid MemberId, DateOnly Date, TimeOnly Start, TimeOnly End, Guid? ScheduleId, Guid? BookingId, BookedBy? By)
{
    /// <summary>A date the student moved to, not one of their fixed schedule's nor one kept as it changed.</summary>
    public bool IsMoved => BookingId is not null && By == BookedBy.Member;
}

/// <param name="Held">The dates the schedule puts its student in a session.</param>
/// <param name="Full">Those of them with no room: the schedule is not saved while there is one.</param>
/// <param name="PassedOver">
/// The dates with no such session, or whose session has started. Saved as taken out
/// (<see cref="SessionSkip.PassOver"/>), so they read as never in the schedule, not as «لغو شد».
/// </param>
public sealed record ScheduleFit(IReadOnlyList<BookedPlace> Held, IReadOnlyList<BookedPlace> Full, IReadOnlyList<BookedPlace> PassedOver);

/// <param name="PassOver">The new schedule's dates to take out (<see cref="SessionSkip.PassOver"/>).</param>
/// <param name="Keep">Today's started sessions to keep as single bookings (<see cref="SessionBooking.Keep"/>).</param>
public sealed record ScheduleHandOver(IReadOnlyList<BookedPlace> PassOver, IReadOnlyList<BookedPlace> Keep);

/// <param name="Students">The members in it.</param>
/// <param name="Removed">The change removes the session or moves its times; else it lowers its capacity below its students.</param>
public sealed record TouchedSession(DateOnly Date, TimeOnly Start, TimeOnly End, IReadOnlyList<Guid> Students, bool Removed);

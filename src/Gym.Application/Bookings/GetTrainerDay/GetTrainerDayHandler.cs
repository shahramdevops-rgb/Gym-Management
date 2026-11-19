using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.GetTrainerDay;

/// <summary>
/// The trainer's day of attendance (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i> and
/// <i>More for the trainer</i>): a day's sessions, who is in each, and whether they checked in at
/// the desk; today by default, or any earlier day (15.7d). Worked out on each read, never stored.
/// </summary>
public sealed class GetTrainerDayHandler(IAppDbContext db, IGymCalendar calendar)
{
    /// <param name="date">The day; <c>null</c> for the gym's today. A later day is refused.</param>
    public async Task<Result<TrainerDayResponse>> Handle(Guid siteAccountId, DateOnly? date, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TrainerDayResponse>(trainer.Error);
        }

        var today = calendar.Today();
        var now = calendar.TimeOfDay();
        var day = date ?? today;
        if (day > today)
        {
            return Result.Failure<TrainerDayResponse>(BookingErrors.AttendanceDayInFuture);
        }

        var trainerId = trainer.Value.Id;
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, day, day, cancellationToken);
        var sessions = timetable.SessionsOn(day);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, day, day, cancellationToken);
        var leaves = PresenceReading.NotBackIn(
            await PresenceReading.LeavesOfTrainerAsync(db, trainerId, day, day, cancellationToken), places);

        // The trainer's students that day, for «آمدند، بدون سانس»: a link that had started and not
        // yet ended (the day a link ends stays theirs, as its sessions do).
        var linked = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.TrainerId == trainerId && link.StartedOn <= day && (link.EndedOn == null || link.EndedOn >= day))
            .Select(link => link.MemberId)
            .ToListAsync(cancellationToken);

        var memberIds = places.Select(place => place.MemberId)
            .Concat(leaves.Select(leave => leave.MemberId))
            .Concat(linked)
            .Distinct()
            .ToList();
        var members = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, cancellationToken);
        var visits = await PresenceReading.VisitsAsync(db, calendar, memberIds, day, day, cancellationToken);

        IEnumerable<DayVisit> VisitsOf(Guid memberId) => visits.GetValueOrDefault((memberId, day)) ?? [];

        DayStudent Student(Guid memberId, PresenceMark mark) => new(
            memberId,
            members[memberId].FullName,
            members[memberId].PhoneNumber,
            mark.State,
            mark.CheckedInAt,
            mark.Visit?.CheckedOutAt,
            mark.Visit?.AutoClosed ?? false,
            mark.MinutesEarly,
            mark.MinutesLate,
            mark.HadLeave);

        List<DayStudent> Sorted(IEnumerable<DayStudent> students) => students.OrderBy(student => student.FullName).ToList();

        var items = sessions
            .Select(session => new DaySession(
                session.Start,
                session.End,
                session.IsVip,
                session.Note,
                timetable.DateNoteOf(day, session),
                Removed: false,
                BookingRules.HasStarted(day, session.Start, today, now),
                BookingRules.HasEnded(day, session.End, today, now),
                Sorted(BookingRules.In(places, day, session)
                    .Select(place => Student(place.MemberId, PresenceRules.Mark(day, session.Start, session.End, VisitsOf(place.MemberId), today, now)))
                    .Concat(leaves
                        .Where(leave => leave.Start == session.Start && leave.End == session.End)
                        .Select(leave => Student(
                            leave.MemberId,
                            PresenceRules.MarkOnLeave(day, session.Start, session.End, VisitsOf(leave.MemberId), today, now)))))))
            .ToList();

        // A session gone from the day's timetable: its students are «لغو شد», those who had left it «مرخصی».
        var gone = places
            .Where(place => BookingRules.SessionOf(place, sessions) is null)
            .Select(place => (place.Start, place.End, Student: Student(place.MemberId, PresenceMark.Without(SessionPresence.Cancelled))))
            .Concat(leaves
                .Where(leave => !sessions.Any(session => session.Start == leave.Start && session.End == leave.End))
                .Select(leave => (leave.Start, leave.End, Student: Student(leave.MemberId, PresenceMark.Without(SessionPresence.Leave)))));

        items.AddRange(gone
            .GroupBy(row => (row.Start, row.End))
            .OrderBy(group => group.Key.Start).ThenBy(group => group.Key.End)
            .Select(group => new DaySession(
                group.Key.Start,
                group.Key.End,
                IsVip: false,
                Note: null,
                DateNote: null,
                Removed: true,
                BookingRules.HasStarted(day, group.Key.Start, today, now),
                BookingRules.HasEnded(day, group.Key.End, today, now),
                Sorted(group.Select(row => row.Student)))));

        var outside = linked
            .Select(memberId =>
            {
                // Only the sessions the student was really in that day, not cancelled; a session they
                // had leave from and came to anyway is theirs too (15.7i).
                var theirs = places
                    .Where(place => place.MemberId == memberId && BookingRules.SessionOf(place, sessions) is not null)
                    .Select(place => (place.Start, place.End))
                    .Concat(leaves
                        .Where(leave => leave.MemberId == memberId && sessions.Any(session => session.Start == leave.Start && session.End == leave.End))
                        .Select(leave => (leave.Start, leave.End)));

                return (MemberId: memberId, Visits: PresenceRules.OutsideSessions(VisitsOf(memberId), theirs));
            })
            .Where(row => row.Visits.Count > 0)
            .Select(row => new OutsideStudent(
                row.MemberId,
                members[row.MemberId].FullName,
                members[row.MemberId].PhoneNumber,
                row.Visits.Select(VisitTimes.Of).ToList()))
            .OrderBy(student => student.FullName)
            .ToList();

        return new TrainerDayResponse(day, day == today, Totals(items), items, outside);
    }

    private static DayTotals Totals(IReadOnlyList<DaySession> sessions)
    {
        var states = sessions
            .SelectMany(session => session.Students)
            .Select(student => student.State)
            .Where(state => state is not (SessionPresence.Leave or SessionPresence.Cancelled))
            .ToList();

        return new DayTotals(
            states.Count,
            states.Count(state => state.IsPresent()),
            states.Count(state => state == SessionPresence.Late),
            states.Count(state => state == SessionPresence.CameAfter),
            states.Count(state => state == SessionPresence.Absent));
    }
}

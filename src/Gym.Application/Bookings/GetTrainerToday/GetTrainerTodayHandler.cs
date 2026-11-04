using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.GetTrainerToday;

/// <summary>
/// The trainer's «امروز» (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>): today's sessions,
/// who is in each, and whether they have checked in at the desk. Worked out on each read, never stored.
/// </summary>
public sealed class GetTrainerTodayHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TrainerTodayResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TrainerTodayResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        var today = calendar.Today();
        var now = calendar.TimeOfDay();

        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, today, today, cancellationToken);
        var sessions = timetable.SessionsOn(today);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, today, today, cancellationToken);
        var leaves = PresenceReading.NotBackIn(
            await PresenceReading.LeavesOfTrainerAsync(db, trainerId, today, today, cancellationToken), places);

        var memberIds = places.Select(place => place.MemberId).Concat(leaves.Select(leave => leave.MemberId)).Distinct().ToList();
        var members = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, cancellationToken);
        var visits = await PresenceReading.VisitsAsync(db, calendar, memberIds, today, today, cancellationToken);

        TodayStudent Student(Guid memberId, PresenceMark mark) =>
            new(memberId, members[memberId].FullName, members[memberId].PhoneNumber, mark.State, mark.CheckedInAt, mark.CameAfter);

        IEnumerable<DayVisit> VisitsOf(Guid memberId) => visits.GetValueOrDefault((memberId, today)) ?? [];

        List<TodayStudent> Sorted(IEnumerable<TodayStudent> students) => students.OrderBy(student => student.FullName).ToList();

        static PresenceMark Fixed(SessionPresence state) => new(state, CheckedInAt: null, CameAfter: false);

        var items = sessions
            .Select(session => new TodaySession(
                session.Start,
                session.End,
                session.IsVip,
                Removed: false,
                BookingRules.HasStarted(today, session.Start, today, now),
                BookingRules.HasEnded(today, session.End, today, now),
                Sorted(BookingRules.In(places, today, session)
                    .Select(place => Student(place.MemberId, PresenceRules.Mark(today, session.Start, session.End, VisitsOf(place.MemberId), today, now)))
                    .Concat(leaves
                        .Where(leave => leave.Start == session.Start && leave.End == session.End)
                        .Select(leave => Student(leave.MemberId, Fixed(SessionPresence.Leave)))))))
            .ToList();

        // A session gone from today's timetable: its students are «لغو شد», those who had left it «مرخصی».
        var gone = places
            .Where(place => BookingRules.SessionOf(place, sessions) is null)
            .Select(place => (place.Start, place.End, Student: Student(place.MemberId, Fixed(SessionPresence.Cancelled))))
            .Concat(leaves
                .Where(leave => !sessions.Any(session => session.Start == leave.Start && session.End == leave.End))
                .Select(leave => (leave.Start, leave.End, Student: Student(leave.MemberId, Fixed(SessionPresence.Leave)))));

        items.AddRange(gone
            .GroupBy(row => (row.Start, row.End))
            .OrderBy(group => group.Key.Start).ThenBy(group => group.Key.End)
            .Select(group => new TodaySession(
                group.Key.Start,
                group.Key.End,
                IsVip: false,
                Removed: true,
                BookingRules.HasStarted(today, group.Key.Start, today, now),
                BookingRules.HasEnded(today, group.Key.End, today, now),
                Sorted(group.Select(row => row.Student)))));

        return new TrainerTodayResponse(today, items);
    }
}

using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.GetStudentAttendance;

/// <summary>
/// The trainer opens a student's attendance (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>):
/// only a student of theirs now, but every session of the range with any trainer, since a new
/// trainer sees the earlier attendance (<i>A member and their trainer</i>).
/// </summary>
public sealed class GetStudentAttendanceHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentAttendanceResponse>> Handle(
        Guid siteAccountId, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentAttendanceResponse>(trainer.Error);
        }

        var link = await StudentSessions.LinkAsync(db, trainer.Value.Id, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<StudentAttendanceResponse>(link.Error);
        }

        if (to < from || to.DayNumber - from.DayNumber + 1 > PresenceRules.MaxRangeDays)
        {
            return Result.Failure<StudentAttendanceResponse>(BookingErrors.AttendanceRangeInvalid);
        }

        var today = calendar.Today();
        var now = calendar.TimeOfDay();
        var last = to < today ? to : today;
        if (last < from)
        {
            return new StudentAttendanceResponse(memberId, from, to, [], new AttendanceTotals(0, 0, 0, 0, 0));
        }

        var places = await BookedPlaces.OfMemberAsync(db, memberId, from, last, cancellationToken);
        var leaves = PresenceReading.NotBackIn(
            await PresenceReading.LeavesOfMemberAsync(db, memberId, from, last, cancellationToken), places);
        var visits = await PresenceReading.VisitsAsync(db, calendar, [memberId], from, last, cancellationToken);

        var trainerIds = places.Select(place => place.TrainerId).Concat(leaves.Select(leave => leave.TrainerId)).Distinct().ToList();
        var timetables = new Dictionary<Guid, TimetableDates>();
        foreach (var trainerId in trainerIds)
        {
            timetables[trainerId] = await TrainerTimetable.DatesAsync(db, trainerId, from, last, cancellationToken);
        }

        var names = await db.Trainers.AsNoTracking()
            .Where(row => trainerIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.FullName, cancellationToken);

        string? NameOf(Guid trainerId) => trainerId == trainer.Value.Id ? null : names[trainerId];

        PlannedSession? SessionAt(Guid trainerId, DateOnly date, TimeOnly start, TimeOnly end) =>
            timetables[trainerId].SessionsOn(date).FirstOrDefault(session => session.Start == start && session.End == end);

        var items = places
            .Select(place =>
            {
                var session = SessionAt(place.TrainerId, place.Date, place.Start, place.End);
                var mark = session is null
                    ? new PresenceMark(SessionPresence.Cancelled, CheckedInAt: null, CameAfter: false)
                    : PresenceRules.Mark(place.Date, place.Start, place.End, visits.GetValueOrDefault((memberId, place.Date)) ?? [], today, now);

                return new AttendanceItem(
                    place.Date, place.Start, place.End, NameOf(place.TrainerId), session?.IsVip ?? false, mark.State, mark.CheckedInAt, mark.CameAfter);
            })
            .Concat(leaves.Select(leave => new AttendanceItem(
                leave.Date,
                leave.Start,
                leave.End,
                NameOf(leave.TrainerId),
                SessionAt(leave.TrainerId, leave.Date, leave.Start, leave.End)?.IsVip ?? false,
                SessionPresence.Leave,
                CheckedInAt: null,
                CameAfter: false)))
            .OrderBy(item => item.Date).ThenBy(item => item.Start).ThenBy(item => item.End)
            .ToList();

        int Count(SessionPresence state) => items.Count(item => item.State == state);

        var totals = new AttendanceTotals(
            Count(SessionPresence.OnTime),
            Count(SessionPresence.Late),
            Count(SessionPresence.Absent),
            Count(SessionPresence.Leave),
            Count(SessionPresence.Cancelled));

        return new StudentAttendanceResponse(memberId, from, to, items, totals);
    }
}

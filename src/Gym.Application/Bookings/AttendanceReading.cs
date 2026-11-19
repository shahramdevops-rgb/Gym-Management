using Gym.Application.Bookings.GetStudentAttendance;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// One member's attendance over a range of dates (BUSINESS_RULES.md §15 <i>Attendance for the
/// trainer</i>): each of their sessions with any trainer up to today, its state, and the totals.
/// Shared by the trainer's «حضور» of a student (15.7b) and the member's own «حضور من» (15.7j), so
/// the two can never tell a session differently.
/// </summary>
internal static class AttendanceReading
{
    /// <param name="from">The first day, already checked against <paramref name="to"/>.</param>
    /// <param name="viewingTrainerId">
    /// The trainer whose sessions show no name: the trainer asking, or the member's trainer now;
    /// <c>null</c> to name every session's trainer.
    /// </param>
    /// <param name="outsideSince">
    /// The first day «خارج از سانس» lists visits from (the trainer's, 15.7d); <c>null</c> lists none.
    /// </param>
    public static async Task<AttendanceRead> ReadAsync(
        IAppDbContext db,
        IGymCalendar calendar,
        Guid memberId,
        DateOnly from,
        DateOnly to,
        Guid? viewingTrainerId,
        DateOnly? outsideSince,
        CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var now = calendar.TimeOfDay();
        var last = to < today ? to : today;
        if (last < from)
        {
            return new AttendanceRead([], new AttendanceTotals(0, 0, 0, 0, 0, 0, 0), []);
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

        string? NameOf(Guid trainerId) => trainerId == viewingTrainerId ? null : names[trainerId];

        PlannedSession? SessionAt(Guid trainerId, DateOnly date, TimeOnly start, TimeOnly end) =>
            timetables[trainerId].SessionsOn(date).FirstOrDefault(session => session.Start == start && session.End == end);

        IEnumerable<DayVisit> VisitsOn(DateOnly date) => visits.GetValueOrDefault((memberId, date)) ?? [];

        AttendanceItem Item(DateOnly date, TimeOnly start, TimeOnly end, Guid trainerId, bool isVip, PresenceMark mark) => new(
            date,
            start,
            end,
            NameOf(trainerId),
            isVip,
            mark.State,
            mark.CheckedInAt,
            mark.Visit?.CheckedOutAt,
            mark.Visit?.AutoClosed ?? false,
            mark.MinutesEarly,
            mark.MinutesLate,
            mark.HadLeave);

        var items = places
            .Select(place =>
            {
                var session = SessionAt(place.TrainerId, place.Date, place.Start, place.End);
                var mark = session is null
                    ? PresenceMark.Without(SessionPresence.Cancelled)
                    : PresenceRules.Mark(place.Date, place.Start, place.End, VisitsOn(place.Date), today, now);

                return Item(place.Date, place.Start, place.End, place.TrainerId, session?.IsVip ?? false, mark);
            })
            .Concat(leaves.Select(leave => Item(
                leave.Date,
                leave.Start,
                leave.End,
                leave.TrainerId,
                SessionAt(leave.TrainerId, leave.Date, leave.Start, leave.End)?.IsVip ?? false,
                SessionAt(leave.TrainerId, leave.Date, leave.Start, leave.End) is null
                    ? PresenceMark.Without(SessionPresence.Leave)
                    : PresenceRules.MarkOnLeave(leave.Date, leave.Start, leave.End, VisitsOn(leave.Date), today, now))))
            .OrderBy(item => item.Date).ThenBy(item => item.Start).ThenBy(item => item.End)
            .ToList();

        int Count(SessionPresence state) => items.Count(item => item.State == state);

        var totals = new AttendanceTotals(
            Count(SessionPresence.Early),
            Count(SessionPresence.OnTime),
            Count(SessionPresence.Late),
            Count(SessionPresence.CameAfter),
            Count(SessionPresence.Absent),
            Count(SessionPresence.Leave),
            Count(SessionPresence.Cancelled));

        if (outsideSince is not { } since)
        {
            return new AttendanceRead(items, totals, []);
        }

        // «خارج از سانس» (15.7d): the check-ins that count for none of the student's sessions that
        // day, from the given day on; a session they had leave from and came to anyway is theirs.
        var firstDay = since > from ? since : from;
        var outside = visits
            .Where(entry => entry.Key.Date >= firstDay)
            .OrderBy(entry => entry.Key.Date)
            .SelectMany(entry =>
            {
                var theirs = places
                    .Where(place => place.Date == entry.Key.Date && SessionAt(place.TrainerId, place.Date, place.Start, place.End) is not null)
                    .Select(place => (place.Start, place.End))
                    .Concat(leaves
                        .Where(leave => leave.Date == entry.Key.Date && SessionAt(leave.TrainerId, leave.Date, leave.Start, leave.End) is not null)
                        .Select(leave => (leave.Start, leave.End)));

                return PresenceRules.OutsideSessions(entry.Value, theirs)
                    .Select(visit => new OutsideVisit(entry.Key.Date, VisitTimes.Of(visit)));
            })
            .ToList();

        return new AttendanceRead(items, totals, outside);
    }

    /// <summary>Whether <paramref name="from"/> to <paramref name="to"/> is a range a screen may ask for.</summary>
    public static bool IsValidRange(DateOnly from, DateOnly to) =>
        to >= from && to.DayNumber - from.DayNumber + 1 <= PresenceRules.MaxRangeDays;
}

/// <summary>What <see cref="AttendanceReading.ReadAsync"/> reads.</summary>
internal sealed record AttendanceRead(IReadOnlyList<AttendanceItem> Items, AttendanceTotals Totals, IReadOnlyList<OutsideVisit> Outside);

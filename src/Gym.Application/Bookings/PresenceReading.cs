using Gym.Application.Common;
using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// What <see cref="PresenceRules"/> needs from the database (task 15.7): the students' check-ins at
/// the desk, by day in the gym's time zone, and the dates they left («نمی‌آیم»).
/// </summary>
internal static class PresenceReading
{
    /// <summary>
    /// The members' check-ins from <paramref name="from"/> to <paramref name="to"/>, by member and
    /// day; a check-in cancelled at the desk is left out (§7: cancelled visits are in no report).
    /// </summary>
    public static async Task<IReadOnlyDictionary<(Guid MemberId, DateOnly Date), List<DayVisit>>> VisitsAsync(
        IAppDbContext db, IGymCalendar calendar, IReadOnlyCollection<Guid> memberIds, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
        {
            return new Dictionary<(Guid, DateOnly), List<DayVisit>>();
        }

        var since = calendar.StartOfDayUtc(from);
        var until = calendar.StartOfDayUtc(to.AddDays(1));

        var rows = await db.Attendances.AsNoTracking()
            .Where(visit => visit.MemberId != null && memberIds.Contains(visit.MemberId.Value))
            .Where(visit => visit.CancelledAt == null && visit.CheckedInAt >= since && visit.CheckedInAt < until)
            .Select(visit => new { MemberId = visit.MemberId!.Value, visit.CheckedInAt, visit.CheckedOutAt, AutoClosed = visit.AutoClosedAt != null })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var checkedIn = TimeZoneInfo.ConvertTime(row.CheckedInAt, calendar.TimeZone);
                var day = DateOnly.FromDateTime(checkedIn.DateTime);
                TimeOnly? left = null;
                TimeOnly? shown = null;
                if (row.CheckedOutAt is { } outAt)
                {
                    var checkedOut = TimeZoneInfo.ConvertTime(outAt, calendar.TimeZone);

                    // Closed on a later day: open for the whole of this one.
                    left = DateOnly.FromDateTime(checkedOut.DateTime) == day ? TimeOnly.FromDateTime(checkedOut.DateTime) : null;

                    // The auto-checkout's midnight is no one's check-out: «خروج ثبت نشد» (15.7d).
                    shown = row.AutoClosed ? null : TimeOnly.FromDateTime(checkedOut.DateTime);
                }

                var visit = new DayVisit(TimeOnly.FromDateTime(checkedIn.DateTime), left) { CheckedOutAt = shown, AutoClosed = row.AutoClosed };

                return (Key: (row.MemberId, day), Visit: visit);
            })
            .GroupBy(row => row.Key, row => row.Visit)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>The dates of the trainer's sessions their students had leave from, from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Task<List<LeftPlace>> LeavesOfTrainerAsync(
        IAppDbContext db, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        LeavesAsync(
            db,
            db.FixedSchedules.Where(schedule => schedule.TrainerId == trainerId),
            db.LeaveRequests.Where(request => request.TrainerId == trainerId),
            from,
            to,
            cancellationToken);

    /// <summary>The dates the member had leave from, with any trainer, from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Task<List<LeftPlace>> LeavesOfMemberAsync(
        IAppDbContext db, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        LeavesAsync(
            db,
            db.FixedSchedules.Where(schedule => schedule.MemberId == memberId),
            db.LeaveRequests.Where(request => request.MemberId == memberId),
            from,
            to,
            cancellationToken);

    /// <summary>The leaves whose student is not in that session after all (put back in it by a single booking): their place decides.</summary>
    public static List<LeftPlace> NotBackIn(IEnumerable<LeftPlace> leaves, IEnumerable<BookedPlace> places)
    {
        var placed = places.Select(place => (place.MemberId, place.Date, place.Start, place.End)).ToHashSet();

        return leaves.Where(leave => !placed.Contains((leave.MemberId, leave.Date, leave.Start, leave.End))).ToList();
    }

    /// <summary>
    /// A leave is a date of a fixed schedule left («نمی‌آیم» before 15.7i, and every accepted
    /// request since, which skips it), or an accepted request on a date the student had moved to,
    /// which removed that booking and left no skip (task 15.7i). Both kinds of one date read once.
    /// </summary>
    private static async Task<List<LeftPlace>> LeavesAsync(
        IAppDbContext db,
        IQueryable<FixedSchedule> schedules,
        IQueryable<LeaveRequest> requests,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var skipped = await db.SessionSkips.AsNoTracking()
            .Where(skip => skip.Reason == SkipReason.Left && skip.Date >= from && skip.Date <= to)
            .Join(
                schedules,
                skip => skip.ScheduleId,
                schedule => schedule.Id,
                (skip, schedule) => new LeftPlace(schedule.TrainerId, schedule.MemberId, skip.Date, skip.Start, skip.End))
            .ToListAsync(cancellationToken);

        var accepted = await requests.AsNoTracking()
            .Where(request => request.Status == LeaveRequestStatus.Accepted && request.Date >= from && request.Date <= to)
            .Select(request => new LeftPlace(request.TrainerId, request.MemberId, request.Date, request.Start, request.End))
            .ToListAsync(cancellationToken);

        return skipped.Concat(accepted).Distinct().ToList();
    }
}

/// <summary>A date of a student's fixed schedule they left themselves: «مرخصی», not absent.</summary>
internal sealed record LeftPlace(Guid TrainerId, Guid MemberId, DateOnly Date, TimeOnly Start, TimeOnly End);

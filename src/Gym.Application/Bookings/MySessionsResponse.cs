using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// «سانس‌های من» on the member's side (BUSINESS_RULES.md §15 <i>Students in sessions</i>): their
/// fixed schedule with their trainer now, the dates they are in from now on with any leave request
/// on them (15.7i), those they have leave from and may take back, and the trainer's sessions they
/// may move a date to. A member sees only their own
/// trainer's sessions.
/// </summary>
/// <param name="Sessions">By date and time: today's sessions that are over are not among them.</param>
/// <param name="TrainerName"><c>null</c> without a trainer now; everything else is empty then.</param>
/// <param name="Schedule">The latest schedule, the one in force or one from a later date.</param>
/// <param name="Openings">
/// The trainer's sessions from now to the schedule's last date that have not started, have room,
/// and the member is not in: where a date can be moved to.
/// </param>
public sealed record MySessionsResponse(
    DateOnly Today,
    string? TrainerName,
    FixedScheduleResponse? Schedule,
    IReadOnlyList<PlaceResponse> Sessions,
    IReadOnlyList<OpeningResponse> Openings)
{
    internal static async Task<MySessionsResponse> ForAsync(
        IAppDbContext db, Guid memberId, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var link = await db.TrainerLinks.AsNoTracking()
            .SingleOrDefaultAsync(row => row.MemberId == memberId && row.EndedOn == null, cancellationToken);
        if (link is null)
        {
            return new MySessionsResponse(today, TrainerName: null, Schedule: null, [], []);
        }

        var trainerName = await db.Trainers.Where(trainer => trainer.Id == link.TrainerId).Select(trainer => trainer.FullName).SingleAsync(cancellationToken);
        var schedules = await db.FixedSchedules.AsNoTracking()
            .Include(schedule => schedule.Slots)
            .Where(schedule => schedule.LinkId == link.Id && schedule.EndsOn >= today)
            .ToListAsync(cancellationToken);
        var latest = schedules.MaxBy(schedule => schedule.StartsOn);

        var lastDay = TimetableRules.LastDay(today);
        var timetable = await TrainerTimetable.DatesAsync(db, link.TrainerId, today, lastDay, cancellationToken);
        var trainerPlaces = await BookedPlaces.OfTrainerAsync(db, link.TrainerId, today, lastDay, cancellationToken);

        var mine = trainerPlaces.Where(place => place.MemberId == memberId).ToList();
        var left = await LeftAsync(db, schedules, mine, timetable, today, timeOfDay, cancellationToken);
        var asked = await AskedAsync(db, link, today, timeOfDay, cancellationToken);
        var sessions = mine
            .Where(place => !BookingRules.HasEnded(place.Date, place.End, today, timeOfDay))
            .Select(place => PlaceResponse.From(place, timetable, today, timeOfDay) with
            {
                Leave = asked.TryGetValue((place.Date, place.Start, place.End), out var leave) ? leave : null,
            })
            .Concat(left)
            .OrderBy(place => place.Date).ThenBy(place => place.Start).ThenBy(place => place.End)
            .ToList();

        List<OpeningResponse> openings = latest is null ? [] : OpeningsUntil(link, schedules.Max(schedule => schedule.EndsOn), timetable, trainerPlaces, today, timeOfDay);

        return new MySessionsResponse(today, trainerName, latest is null ? null : FixedScheduleResponse.From(latest), sessions, openings);
    }

    /// <summary>The last date a member of <paramref name="link"/> may move a session to: their schedules' last; <c>null</c> without one.</summary>
    internal static Task<DateOnly?> LastDateAsync(IAppDbContext db, TrainerLink link, DateOnly today, CancellationToken cancellationToken) =>
        db.FixedSchedules
            .Where(schedule => schedule.LinkId == link.Id && schedule.EndsOn >= today)
            .MaxAsync(schedule => (DateOnly?)schedule.EndsOn, cancellationToken);

    /// <summary>
    /// The dates the member left with «نمی‌آیم» that they may still take back: of their schedules
    /// now, not started, the session still there, and not one they are in again some other way.
    /// Whether it has room is read only when they take it back.
    /// </summary>
    private static async Task<List<PlaceResponse>> LeftAsync(
        IAppDbContext db,
        IReadOnlyCollection<FixedSchedule> schedules,
        IReadOnlyCollection<BookedPlace> mine,
        TimetableDates timetable,
        DateOnly today,
        TimeOnly timeOfDay,
        CancellationToken cancellationToken)
    {
        var byId = schedules.ToDictionary(schedule => schedule.Id);
        var ids = byId.Keys.ToList();
        var skips = await db.SessionSkips.AsNoTracking()
            .Where(skip => ids.Contains(skip.ScheduleId) && skip.Reason == SkipReason.Left && skip.Date >= today)
            .ToListAsync(cancellationToken);

        return skips
            .Where(skip => skip.Date <= byId[skip.ScheduleId].EndsOn && !BookingRules.HasStarted(skip.Date, skip.Start, today, timeOfDay))
            .Where(skip => !mine.Any(place => place.Date == skip.Date && place.Start == skip.Start && place.End == skip.End))
            .Select(skip => (Skip: skip, Session: timetable.SessionsOn(skip.Date).FirstOrDefault(session => session.Start == skip.Start && session.End == skip.End)))
            .Where(left => left.Session is not null)
            .Select(left => PlaceResponse.LeftBy(left.Skip, left.Session!))
            .ToList();
    }

    /// <summary>
    /// The member's leave requests from today on that are still in the session (15.7i), by date and
    /// times: waiting, or declined (by the trainer, or not answered by the start). The latest of a
    /// place, since one declined may be asked again.
    /// </summary>
    private static async Task<Dictionary<(DateOnly Date, TimeOnly Start, TimeOnly End), LeaveRequestStatus>> AskedAsync(
        IAppDbContext db, TrainerLink link, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.LinkId == link.Id && request.Date >= today
                && (request.Status == LeaveRequestStatus.Pending || request.Status == LeaveRequestStatus.Declined))
            .OrderBy(request => request.CreatedAt)
            .ToListAsync(cancellationToken);

        var asked = new Dictionary<(DateOnly, TimeOnly, TimeOnly), LeaveRequestStatus>();
        foreach (var request in requests)
        {
            asked[(request.Date, request.Start, request.End)] = request.StateOn(today, timeOfDay);
        }

        return asked;
    }

    private static List<OpeningResponse> OpeningsUntil(
        TrainerLink link, DateOnly lastDate, TimetableDates timetable, IReadOnlyList<BookedPlace> places, DateOnly today, TimeOnly timeOfDay)
    {
        var openings = new List<OpeningResponse>();
        for (var date = today; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var session in timetable.SessionsOn(date))
            {
                if (BookingRules.CheckJoin(link.MemberId, date, session, places.Where(place => place.Date == date).ToList(), lastDate, today, timeOfDay).IsFailure)
                {
                    continue;
                }

                var taken = BookingRules.In(places, date, session).Count();
                openings.Add(new OpeningResponse(date, session.Start, session.End, session.IsVip, session.Capacity - taken));
            }
        }

        return openings;
    }
}

/// <param name="IsVip">Marked VIP by the trainer (15.6e).</param>
/// <param name="Free">The places left in it.</param>
public sealed record OpeningResponse(DateOnly Date, TimeOnly Start, TimeOnly End, bool IsVip, int Free);

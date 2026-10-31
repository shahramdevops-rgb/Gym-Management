using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// «سانس‌های من» on the member's side (BUSINESS_RULES.md §15 <i>Students in sessions</i>): their
/// fixed schedule with their trainer now, the dates they are in from today on, and the trainer's
/// sessions they may move a date to. A member sees only their own trainer's sessions.
/// </summary>
/// <param name="TrainerName"><c>null</c> without a trainer now; everything else is empty then.</param>
/// <param name="Schedule">The latest schedule, the one in force or the one from tomorrow.</param>
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

        var sessions = trainerPlaces
            .Where(place => place.MemberId == memberId)
            .Select(place => PlaceResponse.From(place, timetable, today, timeOfDay))
            .ToList();

        List<OpeningResponse> openings = latest is null ? [] : OpeningsUntil(link, schedules.Max(schedule => schedule.EndsOn), timetable, trainerPlaces, today, timeOfDay);

        return new MySessionsResponse(today, trainerName, latest is null ? null : FixedScheduleResponse.From(latest), sessions, openings);
    }

    /// <summary>The last date a member of <paramref name="link"/> may move a session to: their schedules' last; <c>null</c> without one.</summary>
    internal static Task<DateOnly?> LastDateAsync(IAppDbContext db, TrainerLink link, DateOnly today, CancellationToken cancellationToken) =>
        db.FixedSchedules
            .Where(schedule => schedule.LinkId == link.Id && schedule.EndsOn >= today)
            .MaxAsync(schedule => (DateOnly?)schedule.EndsOn, cancellationToken);

    private static List<OpeningResponse> OpeningsUntil(
        TrainerLink link, DateOnly lastDate, TimetableDates timetable, IReadOnlyList<BookedPlace> places, DateOnly today, TimeOnly timeOfDay)
    {
        var openings = new List<OpeningResponse>();
        for (var date = today; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var session in timetable.SessionsOn(date))
            {
                if (BookingRules.CheckMoveTarget(link.MemberId, date, session, places.Where(place => place.Date == date).ToList(), lastDate, today, timeOfDay).IsFailure)
                {
                    continue;
                }

                var taken = BookingRules.In(places, date, session).Count();
                openings.Add(new OpeningResponse(date, session.Start, session.End, session.Capacity - taken));
            }
        }

        return openings;
    }
}

/// <param name="Free">The places left in it.</param>
public sealed record OpeningResponse(DateOnly Date, TimeOnly Start, TimeOnly End, int Free);

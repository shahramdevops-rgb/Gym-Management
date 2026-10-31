using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>What the trainer's and the member's handlers share about a student's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>).</summary>
internal static class StudentSessions
{
    /// <summary>The member's open link with the trainer: only the trainer's students now are put in sessions.</summary>
    public static async Task<Result<TrainerLink>> LinkAsync(IAppDbContext db, Guid trainerId, Guid memberId, CancellationToken cancellationToken)
    {
        var link = await db.TrainerLinks.AsNoTracking()
            .SingleOrDefaultAsync(row => row.TrainerId == trainerId && row.MemberId == memberId && row.EndedOn == null, cancellationToken);

        return link is null ? Result.Failure<TrainerLink>(BookingErrors.NotAStudent) : link;
    }

    /// <summary>
    /// The link's fixed schedules from today on, earliest first: the one in force, and the next one
    /// when the trainer changed it from tomorrow (the first then ends today). Tracked, for changing.
    /// </summary>
    public static Task<List<FixedSchedule>> FromTodayAsync(IAppDbContext db, Guid linkId, DateOnly today, CancellationToken cancellationToken) =>
        db.FixedSchedules
            .Include(schedule => schedule.Slots)
            .Where(schedule => schedule.LinkId == linkId && schedule.EndsOn >= today)
            .OrderBy(schedule => schedule.StartsOn)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The trainer changes or ends the schedules from today on: one that has started keeps its dates
    /// up to today, one that has not is removed, its skipped dates with it. The caller saves.
    /// </summary>
    public static void EndAfterToday(IAppDbContext db, IEnumerable<FixedSchedule> schedules, DateOnly today)
    {
        foreach (var schedule in schedules)
        {
            if (schedule.HasStartedBy(today))
            {
                schedule.EndAfter(today);
            }
            else
            {
                db.FixedSchedules.Remove(schedule);
            }
        }
    }

    /// <summary>
    /// Takes the student out of <paramref name="place"/> until the session starts: a date of their
    /// fixed schedule is skipped, a date they moved to is removed. The caller saves.
    /// </summary>
    public static async Task<Result> TakeOutAsync(
        IAppDbContext db, BookedPlace place, BookedBy by, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        if (place.BookingId is { } bookingId)
        {
            var booking = await db.SessionBookings.SingleAsync(row => row.Id == bookingId, cancellationToken);
            var allowed = booking.CheckRemove(today, timeOfDay);
            if (allowed.IsFailure)
            {
                return allowed;
            }

            db.SessionBookings.Remove(booking);

            return Result.Success();
        }

        var skip = SessionSkip.Create(place, by, today, timeOfDay);
        if (skip.IsFailure)
        {
            return skip;
        }

        db.SessionSkips.Add(skip.Value);

        return Result.Success();
    }

    /// <summary>The member's place in the session of <paramref name="date"/> with these times, among the trainer's.</summary>
    public static async Task<Result<BookedPlace>> PlaceAsync(
        IAppDbContext db, Guid trainerId, Guid memberId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken cancellationToken)
    {
        var places = await BookedPlaces.OfMemberAsync(db, memberId, date, date, cancellationToken);
        var place = places.SingleOrDefault(row => row.TrainerId == trainerId && row.Start == start && row.End == end);

        return place is null ? Result.Failure<BookedPlace>(BookingErrors.NotInSession) : place;
    }
}

using System.Linq.Expressions;

using Gym.Application.Common;
using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>Reading the students' places for <see cref="BookingRules.Places"/>: a trainer's, or one member's.</summary>
internal static class BookedPlaces
{
    /// <summary>Every place in the trainer's sessions from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Task<IReadOnlyList<BookedPlace>> OfTrainerAsync(
        IAppDbContext db, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        ReadAsync(db, schedule => schedule.TrainerId == trainerId, booking => booking.TrainerId == trainerId, from, to, cancellationToken);

    /// <summary>The member's places, with any trainer, from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Task<IReadOnlyList<BookedPlace>> OfMemberAsync(
        IAppDbContext db, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        ReadAsync(db, schedule => schedule.MemberId == memberId, booking => booking.MemberId == memberId, from, to, cancellationToken);

    private static async Task<IReadOnlyList<BookedPlace>> ReadAsync(
        IAppDbContext db,
        Expression<Func<FixedSchedule, bool>> whose,
        Expression<Func<SessionBooking, bool>> whoseBooking,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var schedules = await db.FixedSchedules.AsNoTracking()
            .Include(schedule => schedule.Slots)
            .Where(whose)
            .Where(schedule => schedule.StartsOn <= to && schedule.EndsOn >= from)
            .ToListAsync(cancellationToken);

        var scheduleIds = schedules.Select(schedule => schedule.Id).ToList();
        var skips = await db.SessionSkips.AsNoTracking()
            .Where(skip => scheduleIds.Contains(skip.ScheduleId) && skip.Date >= from && skip.Date <= to)
            .ToListAsync(cancellationToken);

        var bookings = await db.SessionBookings.AsNoTracking()
            .Where(whoseBooking)
            .Where(booking => booking.Date >= from && booking.Date <= to)
            .ToListAsync(cancellationToken);

        var linkIds = schedules.Select(schedule => schedule.LinkId).Concat(bookings.Select(booking => booking.LinkId)).Distinct().ToList();
        var linkEnds = await db.TrainerLinks.AsNoTracking()
            .Where(link => linkIds.Contains(link.Id) && link.EndedOn != null)
            .ToDictionaryAsync(link => link.Id, link => link.EndedOn!.Value, cancellationToken);

        return BookingRules.Places(from, to, schedules, linkEnds, skips, bookings);
    }
}

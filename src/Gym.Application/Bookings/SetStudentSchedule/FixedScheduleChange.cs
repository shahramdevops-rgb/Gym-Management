using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.SetStudentSchedule;

/// <summary>
/// Saving a student's fixed schedule, up to the room check (BUSINESS_RULES.md §15 <i>Students in
/// sessions</i>): shared by «ذخیره برنامه» and the check before it, which rolls it back.
/// </summary>
/// <remarks>
/// The new schedule is written before its room is counted, so the count reads the places exactly as
/// they would be: the old schedule stopped from today or removed, the new one in. The caller holds
/// the trainer's lock and a transaction, and commits only when no date is full.
/// </remarks>
internal static class FixedScheduleChange
{
    public static async Task<Result<(TrainerLink Link, ScheduleFit Fit)>> ApplyAsync(
        IAppDbContext db, Guid trainerId, Guid memberId, SetStudentScheduleCommand command, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var link = await StudentSessions.LinkAsync(db, trainerId, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(link.Error);
        }

        var slots = command.Slots.Select(slot => new WeeklySlot(slot.Day, slot.Start, slot.End)).ToList();
        var created = FixedSchedule.Create(trainerId, memberId, link.Value.Id, command.StartsOn, command.EndsOn, slots, today);
        if (created.IsFailure)
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(created.Error);
        }

        // The change applies from now (decided by the developer, 1405/08/09): what has started today
        // stays as it was, read before the old schedule stops.
        var current = await StudentSessions.FromTodayAsync(db, link.Value.Id, today, cancellationToken);
        var started = await StudentSessions.StartedTodayAsync(db, current, today, timeOfDay, cancellationToken);

        // Saved before the new one goes in: the exclusion constraint holds one schedule per link at
        // every write.
        StudentSessions.StopFromToday(db, current, today);
        await db.SaveChangesAsync(cancellationToken);

        var schedule = created.Value;
        db.FixedSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, schedule.StartsOn, schedule.EndsOn, cancellationToken);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, schedule.StartsOn, schedule.EndsOn, cancellationToken);
        var fit = BookingRules.Fit(schedule, places.ToList(), timetable.SessionsOn, today, timeOfDay);
        if (fit.IsFailure)
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(fit.Error);
        }

        // The dates it passes over are taken out now, so a session that comes or goes later does not
        // make them read as «لغو شد»; today's started sessions it does not have are kept on their own.
        var handOver = BookingRules.HandOver(started, fit.Value.PassedOver);
        db.SessionSkips.AddRange(handOver.PassOver.Select(SessionSkip.PassOver));
        db.SessionBookings.AddRange(handOver.Keep.Select(place => SessionBooking.Keep(place, link.Value.Id)));
        await db.SaveChangesAsync(cancellationToken);

        return (link.Value, fit.Value);
    }
}

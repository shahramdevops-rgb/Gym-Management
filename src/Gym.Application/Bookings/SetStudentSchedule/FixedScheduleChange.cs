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
/// they would be: the old schedule ended today or removed, the new one in. The caller holds the
/// trainer's lock and a transaction, and commits only when no date is full.
/// </remarks>
internal static class FixedScheduleChange
{
    public static async Task<Result<(TrainerLink Link, ScheduleFit Fit)>> ApplyAsync(
        IAppDbContext db, Guid trainerId, Guid memberId, SetStudentScheduleCommand command, DateOnly today, CancellationToken cancellationToken)
    {
        var link = await StudentSessions.LinkAsync(db, trainerId, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(link.Error);
        }

        // A schedule that has started changes from tomorrow: today's sessions stay as they were.
        var current = await StudentSessions.FromTodayAsync(db, link.Value.Id, today, cancellationToken);
        if (command.StartsOn <= today && current.Any(schedule => schedule.HasStartedBy(today)))
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(BookingErrors.ScheduleChangeFromTomorrow);
        }

        var slots = command.Slots.Select(slot => new WeeklySlot(slot.Day, slot.Start, slot.End)).ToList();
        var created = FixedSchedule.Create(trainerId, memberId, link.Value.Id, command.StartsOn, command.EndsOn, slots, today);
        if (created.IsFailure)
        {
            return Result.Failure<(TrainerLink, ScheduleFit)>(created.Error);
        }

        // Saved before the new one goes in: the exclusion constraint holds one schedule per link at
        // every write.
        StudentSessions.EndAfterToday(db, current, today);
        await db.SaveChangesAsync(cancellationToken);

        var schedule = created.Value;
        db.FixedSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, schedule.StartsOn, schedule.EndsOn, cancellationToken);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, schedule.StartsOn, schedule.EndsOn, cancellationToken);
        var fit = BookingRules.Fit(schedule, places.ToList(), timetable.SessionsOn);

        return fit.IsFailure ? Result.Failure<(TrainerLink, ScheduleFit)>(fit.Error) : (link.Value, fit.Value);
    }
}

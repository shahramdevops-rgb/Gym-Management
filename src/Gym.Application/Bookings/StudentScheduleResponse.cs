using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Coaching;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// One student's sessions on the trainer's side (BUSINESS_RULES.md §15 <i>Students in sessions</i>):
/// their «برنامهٔ ثابت» to edit, and every date they are in from today to the timetable's last day.
/// </summary>
/// <param name="Schedule">The latest schedule, the one in force or one from a later date; <c>null</c> when none reaches today.</param>
/// <param name="PeriodEndsOn">
/// The coaching period's last day (15.7h): a schedule ends on it or before. <c>null</c> for a link
/// made before 15.7h whose days left the trainer has not typed yet.
/// </param>
public sealed record StudentScheduleResponse(
    DateOnly Today,
    DateOnly LastDay,
    Guid MemberId,
    string FullName,
    DateOnly? PeriodEndsOn,
    FixedScheduleResponse? Schedule,
    IReadOnlyList<PlaceResponse> Sessions)
{
    internal static async Task<StudentScheduleResponse> ForAsync(
        IAppDbContext db, TrainerLink link, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var lastDay = TimetableRules.LastDay(today);
        var periodEndsOn = await CoachingPeriods.EndsOnAsync(db, link.Id, cancellationToken);
        var fullName = await db.Members.Where(member => member.Id == link.MemberId).Select(member => member.FullName).SingleAsync(cancellationToken);

        var latest = await db.FixedSchedules.AsNoTracking()
            .Include(schedule => schedule.Slots)
            .Where(schedule => schedule.LinkId == link.Id && schedule.EndsOn >= today)
            .OrderByDescending(schedule => schedule.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);

        var timetable = await TrainerTimetable.DatesAsync(db, link.TrainerId, today, lastDay, cancellationToken);
        var places = await BookedPlaces.OfMemberAsync(db, link.MemberId, today, lastDay, cancellationToken);
        var sessions = places
            .Where(place => place.TrainerId == link.TrainerId)
            .Select(place => PlaceResponse.From(place, timetable, today, timeOfDay))
            .ToList();

        return new StudentScheduleResponse(
            today, lastDay, link.MemberId, fullName, periodEndsOn, latest is null ? null : FixedScheduleResponse.From(latest), sessions);
    }
}

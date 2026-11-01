using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables;

/// <summary>
/// The warning before a change of the timetable (BUSINESS_RULES.md §15 <i>Students in sessions</i>):
/// the sessions with students that the change removes, moves, or leaves with too little room. The
/// trainer then saves anyway or not; nothing here refuses the change.
/// </summary>
/// <param name="Students">How many students it touches, each once.</param>
/// <param name="ToReschedule">
/// The students whose fixed schedule loses sessions to the change, by name. After a change of the
/// pattern their trainer gives them a new one (roadmap 15.6b-4); a single date's change only cancels
/// that date. A session whose capacity drops keeps its students, so they are not here.
/// </param>
public sealed record TimetableImpactResponse(
    int Students,
    IReadOnlyList<TouchedSessionResponse> Sessions,
    IReadOnlyList<StudentToRescheduleResponse> ToReschedule)
{
    /// <param name="from">The first date the change reaches.</param>
    /// <param name="to">The last date the change reaches.</param>
    /// <param name="after">A date's sessions after the change.</param>
    internal static async Task<TimetableImpactResponse> ForAsync(
        IAppDbContext db,
        Guid trainerId,
        DateOnly from,
        DateOnly to,
        TimetableDates timetable,
        Func<DateOnly, IReadOnlyList<PlannedSession>> after,
        CancellationToken cancellationToken)
    {
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, from, to, cancellationToken);
        var touched = BookingRules.Impact(places.ToList(), timetable.SessionsOn, after);

        var rescheduleIds = touched
            .Where(session => session.Removed)
            .SelectMany(session => session.OnSchedule)
            .Distinct()
            .ToList();
        var toReschedule = await db.Members.AsNoTracking()
            .Where(member => rescheduleIds.Contains(member.Id))
            .OrderBy(member => member.FullName)
            .Select(member => new StudentToRescheduleResponse(member.Id, member.FullName))
            .ToListAsync(cancellationToken);

        return new TimetableImpactResponse(
            touched.SelectMany(session => session.Students).Distinct().Count(),
            touched.Select(session => new TouchedSessionResponse(session.Date, session.Start, session.End, session.Students.Count, session.Removed)).ToList(),
            toReschedule);
    }

    /// <summary>The warning of a change that reaches no booked date.</summary>
    internal static TimetableImpactResponse None { get; } = new(0, [], []);
}

/// <param name="Removed">The change removes the session or moves its times; else its new capacity is below <paramref name="Students"/>.</param>
public sealed record TouchedSessionResponse(DateOnly Date, TimeOnly Start, TimeOnly End, int Students, bool Removed);

public sealed record StudentToRescheduleResponse(Guid MemberId, string FullName);

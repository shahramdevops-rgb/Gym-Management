using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables;

/// <summary>
/// The warning before a change of the timetable (BUSINESS_RULES.md §15 <i>Students in sessions</i>):
/// the sessions with students that the change removes, moves, or leaves with too little room. The
/// trainer then saves anyway or not; nothing here refuses the change.
/// </summary>
/// <param name="Students">How many students it touches, each once.</param>
public sealed record TimetableImpactResponse(int Students, IReadOnlyList<TouchedSessionResponse> Sessions)
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

        return new TimetableImpactResponse(
            touched.SelectMany(session => session.Students).Distinct().Count(),
            touched.Select(session => new TouchedSessionResponse(session.Date, session.Start, session.End, session.Students.Count, session.Removed)).ToList());
    }
}

/// <param name="Removed">The change removes the session or moves its times; else its new capacity is below <paramref name="Students"/>.</param>
public sealed record TouchedSessionResponse(DateOnly Date, TimeOnly Start, TimeOnly End, int Students, bool Removed);

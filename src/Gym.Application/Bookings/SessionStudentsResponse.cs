using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// «شاگردان سانس»: each session of one date with its students (BUSINESS_RULES.md §15 <i>Students in
/// sessions</i>), then the students whose session the trainer has since removed, under its old times.
/// The trainer sees each student's name and phone, as in «شاگردان من».
/// </summary>
public sealed record SessionStudentsResponse(DateOnly Date, IReadOnlyList<SessionStudentsItem> Sessions)
{
    internal static async Task<SessionStudentsResponse> ForAsync(
        IAppDbContext db, Guid trainerId, DateOnly date, DateOnly today, TimeOnly timeOfDay, CancellationToken cancellationToken)
    {
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);
        var sessions = timetable.SessionsOn(date);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, date, date, cancellationToken);

        var memberIds = places.Select(place => place.MemberId).Distinct().ToList();
        var members = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, cancellationToken);

        List<SessionStudentResponse> StudentsOf(IEnumerable<BookedPlace> inIt) => inIt
            .Select(place => new SessionStudentResponse(place.MemberId, members[place.MemberId].FullName, members[place.MemberId].PhoneNumber, place.IsMoved))
            .OrderBy(student => student.FullName)
            .ToList();

        var items = sessions
            .Select(session => new SessionStudentsItem(
                session.Start,
                session.End,
                session.Capacity,
                session.IsVip,
                session.Note,
                timetable.DateNoteOf(date, session),
                Removed: false,
                BookingRules.HasStarted(date, session.Start, today, timeOfDay),
                BookingRules.HasEnded(date, session.End, today, timeOfDay),
                StudentsOf(BookingRules.In(places, date, session))))
            .ToList();

        items.AddRange(places
            .Where(place => BookingRules.SessionOf(place, sessions) is null)
            .GroupBy(place => (place.Start, place.End))
            .OrderBy(group => group.Key.Start).ThenBy(group => group.Key.End)
            .Select(group => new SessionStudentsItem(
                group.Key.Start,
                group.Key.End,
                Capacity: null,
                IsVip: false,
                Note: null,
                DateNote: null,
                Removed: true,
                BookingRules.HasStarted(date, group.Key.Start, today, timeOfDay),
                BookingRules.HasEnded(date, group.Key.End, today, timeOfDay),
                StudentsOf(group))));

        return new SessionStudentsResponse(date, items);
    }
}

/// <param name="Capacity"><c>null</c> for a session removed since its students were put in it.</param>
/// <param name="IsVip">Marked VIP by the trainer (15.6e); never for a removed session.</param>
/// <param name="Note">The session's standing note (15.7e); never for a removed session.</param>
/// <param name="DateNote">The trainer's note on this date's session alone (15.7e); never for a removed session.</param>
/// <param name="Removed">The trainer removed the session, or moved its times, after these students were put in it.</param>
/// <param name="Started">It has started: no one is taken out of it any more.</param>
/// <param name="Ended">It is over: its date's note no longer changes (15.7e).</param>
public sealed record SessionStudentsItem(
    TimeOnly Start,
    TimeOnly End,
    int? Capacity,
    bool IsVip,
    string? Note,
    string? DateNote,
    bool Removed,
    bool Started,
    bool Ended,
    IReadOnlyList<SessionStudentResponse> Students);

/// <param name="Moved">Here for this date only: the student moved one of their sessions to it.</param>
public sealed record SessionStudentResponse(Guid MemberId, string FullName, string PhoneNumber, bool Moved);

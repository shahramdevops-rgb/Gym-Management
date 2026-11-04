using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Notifications;

namespace Gym.Application.Timetables;

/// <summary>
/// What a saved change of the timetable cancelled (BUSINESS_RULES.md §15 <i>Timetable and
/// attendance</i>, task 15.6c): every student's session it removed or moved the times of, the same
/// sessions as the warning before it (<see cref="TimetableImpactResponse"/>). A session whose capacity
/// drops keeps its students, so it cancels nothing.
/// </summary>
internal static class TimetableCancellations
{
    /// <summary>
    /// Writes a <see cref="SessionCancellation"/> for each student's session the change removed, all
    /// under one new change id. Called after the change is saved, in its transaction and under the
    /// trainer's lock, with the timetable as it was read before the change. The caller saves.
    /// </summary>
    /// <param name="before">The trainer's timetable over <paramref name="from"/> to <paramref name="to"/> before the change.</param>
    /// <returns>The change and the students to send its SMS to: those with a date within the SMS's days.</returns>
    public static async Task<CancelledSessions> RecordAsync(
        IAppDbContext db,
        Guid trainerId,
        DateOnly from,
        DateOnly to,
        TimetableDates before,
        DateOnly today,
        TimeOnly timeOfDay,
        CancellationToken cancellationToken)
    {
        var after = await TrainerTimetable.DatesAsync(db, trainerId, from, to, cancellationToken);
        var places = await BookedPlaces.OfTrainerAsync(db, trainerId, from, to, cancellationToken);

        var removed = BookingRules.Impact(places.ToList(), before.SessionsOn, after.SessionsOn)
            .Where(session => session.Removed)
            .ToList();
        if (removed.Count == 0)
        {
            return CancelledSessions.None;
        }

        var changeId = Guid.CreateVersion7();
        var rows = removed
            .SelectMany(session => session.Students.Distinct()
                .Select(memberId => SessionCancellation.Create(memberId, trainerId, changeId, session.Date, session.Start, session.End)))
            .ToList();
        db.SessionCancellations.AddRange(rows);

        var smsTo = rows
            .Where(row => row.IsSmsDue(today, timeOfDay, SmsSettings.SessionCancelledDaysAhead))
            .Select(row => row.MemberId)
            .Distinct()
            .ToList();

        return new CancelledSessions(changeId, smsTo);
    }
}

/// <summary>One saved change's cancellations: its id, and the students its SMS goes to.</summary>
internal sealed record CancelledSessions(Guid ChangeId, IReadOnlyList<Guid> SmsTo)
{
    public static CancelledSessions None { get; } = new(Guid.Empty, []);
}

using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;

namespace Gym.Application.Bookings;

/// <summary>
/// Which of one trainer's <see cref="SessionCancellation"/> rows are still true (BUSINESS_RULES.md §15
/// <i>Timetable and attendance</i>, task 15.6c): the trainer has not brought the session back since,
/// with the same times on the same date. The app and the SMS tell only those.
/// </summary>
internal static class StillCancelled
{
    /// <param name="rows">One trainer's rows, of today or later.</param>
    public static async Task<List<SessionCancellation>> FilterAsync(
        IAppDbContext db, Guid trainerId, IReadOnlyCollection<SessionCancellation> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, rows.Min(row => row.Date), rows.Max(row => row.Date), cancellationToken);

        return rows
            .Where(row => !timetable.SessionsOn(row.Date).Any(session => session.Start == row.Start && session.End == row.End))
            .ToList();
    }
}

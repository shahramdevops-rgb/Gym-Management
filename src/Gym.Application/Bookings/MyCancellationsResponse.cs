using Gym.Application.Common;
using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings;

/// <summary>
/// «سانس لغو شد» on the member's home (BUSINESS_RULES.md §15 <i>Timetable and attendance</i>, task
/// 15.6c): their trainer's cancelled sessions they have not pressed «دیدم» for, from today on, and
/// only those the trainer has not brought back. A session cancelled twice is listed once.
/// </summary>
/// <param name="Sessions">By date and time; empty when there is nothing to tell.</param>
/// <param name="Latest">
/// The latest cancellation read, a moment (UTC): «دیدم» sends it back, so a cancellation saved after
/// the page was read stays to be seen. <c>null</c> with nothing listed.
/// </param>
public sealed record MyCancellationsResponse(IReadOnlyList<SessionDateResponse> Sessions, DateTimeOffset? Latest)
{
    internal static async Task<MyCancellationsResponse> ForAsync(IAppDbContext db, Guid memberId, DateOnly today, CancellationToken cancellationToken)
    {
        // Only the trainer now: a member sees only their own trainer's sessions.
        var trainerId = await db.TrainerLinks
            .Where(link => link.MemberId == memberId && link.EndedOn == null)
            .Select(link => (Guid?)link.TrainerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (trainerId is null)
        {
            return new MyCancellationsResponse([], Latest: null);
        }

        var rows = await db.SessionCancellations.AsNoTracking()
            .Where(row => row.MemberId == memberId && row.TrainerId == trainerId && row.SeenAt == null && row.Date >= today)
            .ToListAsync(cancellationToken);
        var still = await StillCancelled.FilterAsync(db, trainerId.Value, rows, cancellationToken);
        if (still.Count == 0)
        {
            return new MyCancellationsResponse([], Latest: null);
        }

        var sessions = still
            .Select(row => new SessionDateResponse(row.Date, row.Start, row.End))
            .Distinct()
            .OrderBy(session => session.Date).ThenBy(session => session.Start).ThenBy(session => session.End)
            .ToList();

        return new MyCancellationsResponse(sessions, rows.Max(row => row.CreatedAt));
    }
}

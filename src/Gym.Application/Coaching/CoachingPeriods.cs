using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>Reading a link's coaching period (BUSINESS_RULES.md §15 <i>The coaching period</i>, 15.7h).</summary>
internal static class CoachingPeriods
{
    /// <summary>The last day of the link's last period; <c>null</c> for a link made before 15.7h that has none yet.</summary>
    public static Task<DateOnly?> EndsOnAsync(IAppDbContext db, Guid linkId, CancellationToken cancellationToken) =>
        db.CoachingPeriods
            .Where(period => period.LinkId == linkId)
            .MaxAsync(period => (DateOnly?)period.EndsOn, cancellationToken);

    /// <summary>
    /// Nothing of the link's sessions is left after <paramref name="lastDay"/>, since no session is
    /// placed after a period: the fixed schedule in force ends on it, one that starts after it goes,
    /// and so do the dates the student moved to after it. For the days left of a link made before
    /// 15.7h, and for «ویرایش دوره» that ends the coaching earlier (15.7k). The caller saves.
    /// </summary>
    public static async Task CutAfterAsync(IAppDbContext db, Guid linkId, DateOnly lastDay, CancellationToken cancellationToken)
    {
        var schedules = await db.FixedSchedules
            .Where(schedule => schedule.LinkId == linkId && schedule.EndsOn > lastDay)
            .ToListAsync(cancellationToken);
        foreach (var schedule in schedules)
        {
            if (schedule.StartsAfter(lastDay))
            {
                db.FixedSchedules.Remove(schedule);
            }
            else
            {
                schedule.EndOnPeriodEnd(lastDay);
            }
        }

        var moved = await db.SessionBookings
            .Where(booking => booking.LinkId == linkId && booking.Date > lastDay)
            .ToListAsync(cancellationToken);
        db.SessionBookings.RemoveRange(moved);
    }
}

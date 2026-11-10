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
}

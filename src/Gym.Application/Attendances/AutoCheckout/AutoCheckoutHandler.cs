using Gym.Application.Common;
using Gym.Domain.Attendances;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.AutoCheckout;

/// <summary>
/// The nightly job (BUSINESS_RULES.md §7 Auto-checkout): closes every attendance still open at
/// <c>Gym:ClosingTime</c> and marks it auto-closed. The session stays consumed — unlike
/// <see cref="CancelCheckIn.CancelCheckInHandler"/>, nothing here touches a subscription.
/// </summary>
/// <remarks>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs schedules it directly with Hangfire.
/// Loading the rows as tracked entities (rather than a set-based update) means this goes through
/// the same <c>SaveChangesAsync</c> path as every other write, so the audit interceptor stamps
/// it the same way — <c>UpdatedBy</c> is simply null, which <c>AuditableEntityInterceptor</c>
/// already documents as the expected value for background jobs.
/// </remarks>
public sealed class AutoCheckoutHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        var open = await db.Attendances.Where(a => a.CheckedOutAt == null).ToListAsync(cancellationToken);
        if (open.Count == 0)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        foreach (var attendance in open)
        {
            attendance.AutoClose(now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return open.Count;
    }
}

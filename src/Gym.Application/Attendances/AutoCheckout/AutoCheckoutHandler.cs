using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.AutoCheckout;

/// <summary>
/// The nightly job (BUSINESS_RULES.md §7 Auto-checkout): closes every attendance still open at
/// <c>Gym:ClosingTime</c> and marks it auto-closed. The session stays consumed — unlike
/// <see cref="CancelCheckIn.CancelCheckInHandler"/>, nothing here touches a subscription. A
/// cardio-only visit with no هوازی amount is left open (§7 <i>Cardio-only visit</i>): the desk
/// records its amount and checks it out the next day.
/// </summary>
/// <remarks>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs schedules it directly with Hangfire.
/// Loading the rows as tracked entities (rather than a set-based update) means this goes through
/// the same <c>SaveChangesAsync</c> path as every other write, so the audit interceptor stamps
/// it the same way — <c>UpdatedBy</c> is simply null, which <c>AuditableEntityInterceptor</c>
/// already documents as the expected value for background jobs.
/// A visit the desk closes or moves while this runs changes its <c>xmin</c>, and the save then
/// throws rather than overwrite it; the job fails and Hangfire runs it again, which closes whatever
/// is still open by then.
/// </remarks>
public sealed class AutoCheckoutHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        var open = await db.Attendances
            .Where(a => a.CheckedOutAt == null)
            .Where(a => !a.IsCardioOnly || db.ServiceCharges.Any(charge =>
                charge.AttendanceId == a.Id && charge.Kind == ServiceChargeKind.Cardio && charge.VoidedAt == null))
            .ToListAsync(cancellationToken);
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

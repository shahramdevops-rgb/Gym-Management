using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.AutoCheckout;

/// <summary>
/// The nightly job (BUSINESS_RULES.md §7 Auto-checkout): closes every attendance still open at
/// <c>Gym:ClosingTime</c> and marks it auto-closed. The session stays consumed — unlike
/// <see cref="CancelCheckIn.CancelCheckInHandler"/>, nothing here touches a subscription. Two kinds
/// of visit are left open for the desk the next day: a cardio-only visit with no هوازی amount
/// (§7 <i>Cardio-only visit</i>), and a guest's visit with anything still unpaid (§7 <i>Guest
/// visit</i>), which keeps its locker «بدهکار» until the guest's purchases are settled.
/// </summary>
/// <remarks>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs schedules it directly with Hangfire.
/// Loading the rows as tracked entities (rather than a set-based update) means this goes through
/// the same <c>SaveChangesAsync</c> path as every other write, so the audit interceptor stamps
/// it the same way — <c>UpdatedBy</c> is simply null, which <c>AuditableEntityInterceptor</c>
/// already documents as the expected value for background jobs.
/// A guest's visit is locked before its purchases are read, the lock its check-out takes, so an
/// order, a charge or a payment for it cannot land between the read and the close.
/// A visit the desk closes or moves while this runs changes its <c>xmin</c>, and the save then
/// throws rather than overwrite it; the job fails and Hangfire runs it again, which closes whatever
/// is still open by then.
/// </remarks>
public sealed class AutoCheckoutHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var open = await db.Attendances
            .Where(a => a.CheckedOutAt == null)
            .Where(a => !a.IsCardioOnly || db.ServiceCharges.Any(charge =>
                charge.AttendanceId == a.Id && charge.Kind == ServiceChargeKind.Cardio && charge.VoidedAt == null))
            .ToListAsync(cancellationToken);

        var toClose = new List<Attendance>();
        foreach (var attendance in open)
        {
            if (attendance.IsGuest)
            {
                await db.LockAttendanceAsync(attendance.Id, cancellationToken);
                if ((await GuestPurchases.UnpaidAsync(db, attendance.Id, cancellationToken)).Any)
                {
                    continue;
                }
            }

            toClose.Add(attendance);
        }

        if (toClose.Count == 0)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        foreach (var attendance in toClose)
        {
            attendance.AutoClose(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return toClose.Count;
    }
}

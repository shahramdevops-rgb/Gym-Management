using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances;

/// <summary>
/// Taken before anything is bought on a guest's visit (BUSINESS_RULES.md §7 <i>Guest visit</i>). The
/// guest's check-out takes the same lock and refuses while a purchase is unpaid, so a purchase must
/// not land on a visit that closed while the request was on its way.
/// </summary>
public static class GuestVisitLock
{
    /// <summary>
    /// Locks the visit inside the caller's transaction and says whether it is still open, asked
    /// again under the lock rather than trusted from an earlier read.
    /// </summary>
    public static async Task<bool> TakeAndCheckOpenAsync(
        IAppDbContext db, Guid attendanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        await db.LockAttendanceAsync(attendanceId, cancellationToken);

        return await db.Attendances.AnyAsync(
            a => a.Id == attendanceId && a.CheckedOutAt == null, cancellationToken);
    }
}

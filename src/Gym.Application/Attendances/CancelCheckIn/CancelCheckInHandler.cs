using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CancelCheckIn;

/// <summary>
/// Cancels an open check-in within the allowed window (BUSINESS_RULES.md §7): restores the
/// session and frees the locker (occupancy is derived from <c>CheckedOutAt</c>, which
/// <see cref="Attendance.Cancel"/> also sets). Front desk work, so both roles.
/// </summary>
public sealed class CancelCheckInHandler(IAppDbContext db, IAttendancePolicy policy, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NotFound);
        }

        var cancelled = attendance.Cancel(time.GetUtcNow(), policy.CancelWindowMinutes);
        if (cancelled.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(cancelled.Error);
        }

        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == attendance.SubscriptionId, cancellationToken);
        subscription.RestoreSession();

        await db.SaveChangesAsync(cancellationToken);

        var lockerNumber = attendance.LockerId is null
            ? null
            : await db.Lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).SingleOrDefaultAsync(cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber);
    }
}

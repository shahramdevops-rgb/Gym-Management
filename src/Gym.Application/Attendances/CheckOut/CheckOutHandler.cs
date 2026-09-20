using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.CheckOut;

/// <summary>
/// Closes an open attendance, which frees its locker (occupancy is derived from
/// <c>CheckedOutAt</c>, BUSINESS_RULES.md §6/§7). Front desk work, so both roles.
/// </summary>
public sealed class CheckOutHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NotFound);
        }

        var checkedOut = attendance.CheckOut(time.GetUtcNow());
        if (checkedOut.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(checkedOut.Error);
        }

        await db.SaveChangesAsync(cancellationToken);

        var lockerNumber = attendance.LockerId is null
            ? null
            : await db.Lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).SingleOrDefaultAsync(cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber);
    }
}

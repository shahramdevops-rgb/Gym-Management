using Gym.Application.Common;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.MoveLocker;

/// <summary>
/// Moves an open visit to another locker: the desk gave the wrong one, or it broke while in use
/// (BUSINESS_RULES.md §7 <i>Moving to another locker</i>). A visit on a reserve place gives the place
/// up. Front desk work, so both roles.
/// </summary>
/// <remarks>
/// No session changes hands and the visit's هوازی and cafe orders stay where they are: they hang off
/// the attendance, not the locker. The old locker is free as soon as this saves, because occupancy
/// is derived from the open visit. The audit log records the change like any other update.
/// </remarks>
public sealed class MoveLockerHandler(IAppDbContext db)
{
    public async Task<Result<AttendanceResponse>> Handle(Guid id, MoveLockerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.NotFound);
        }

        // The visit's own rules first (open, not already there): a closed visit is "already closed"
        // whichever locker was picked. Nothing is saved if the target check below fails.
        var moved = attendance.MoveToLocker(command.LockerId);
        if (moved.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(moved.Error);
        }

        var lockerNumber = await LockerChoice.CheckAsync(db, command.LockerId, cancellationToken);
        if (lockerNumber.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(lockerNumber.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == AttendanceConstraints.OneOpenPerLocker)
        {
            // Another desk gave the target out a moment earlier: the same answer as check-in's.
            return Result.Failure<AttendanceResponse>(AttendanceErrors.LockerTaken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The visit was closed or moved by someone else since it was read (its xmin changed).
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }

        var charges = await VisitServiceCharges.ByAttendanceAsync(db, [id], cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber.Value, charges.GetValueOrDefault(id, []));
    }
}

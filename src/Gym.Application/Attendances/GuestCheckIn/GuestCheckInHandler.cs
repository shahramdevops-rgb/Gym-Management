using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.GuestCheckIn;

/// <summary>
/// Lets a guest in: a visit with a typed name, no member and no subscription, holding the locker the
/// desk chose or a reserve place (BUSINESS_RULES.md §7 <i>Guest visit</i>). Nothing is sold and no
/// session is consumed. Front desk work, so both roles; the audit log records who let the guest in.
/// </summary>
/// <remarks>
/// The place goes through the same checks as a member's check-in (<see cref="LockerChoice"/>), and
/// the same partial unique indexes settle a race for it. There is no "already inside" check: a
/// guest is not a person the system knows, so the same name twice is two guests.
/// </remarks>
public sealed class GuestCheckInHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<AttendanceResponse>> Handle(GuestCheckInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = time.GetUtcNow();
        int? lockerNumber = null;
        Result<Attendance> created;
        if (command.LockerId is { } lockerId)
        {
            var locker = await LockerChoice.CheckAsync(db, lockerId, cancellationToken);
            if (locker.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(locker.Error);
            }

            lockerNumber = locker.Value;
            created = Attendance.CheckInGuest(command.GuestName, lockerId, now);
        }
        else
        {
            var reserveSlot = await LockerChoice.FreeReserveSlotAsync(db, cancellationToken);
            if (reserveSlot.IsFailure)
            {
                return Result.Failure<AttendanceResponse>(reserveSlot.Error);
            }

            created = Attendance.CheckInGuestOnReservePlace(command.GuestName, reserveSlot.Value, now);
        }

        if (created.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(created.Error);
        }

        var attendance = created.Value;
        db.Attendances.Add(attendance);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == AttendanceConstraints.OneOpenPerLocker)
        {
            // Another desk gave out the same locker a moment earlier: the same answer as check-in's.
            return Result.Failure<AttendanceResponse>(AttendanceErrors.LockerTaken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == AttendanceConstraints.OneOpenPerReserveSlot)
        {
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }

        return AttendanceResponse.From(attendance, lockerNumber);
    }
}

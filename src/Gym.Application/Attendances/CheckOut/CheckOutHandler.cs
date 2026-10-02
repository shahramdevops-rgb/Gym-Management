using Gym.Application.Common;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

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

        // A guest cannot leave with the cafe unpaid (BUSINESS_RULES.md §7 Guest visit). The visit's
        // lock keeps an order or a payment for it from landing between the check and the close; a
        // member's check-out needs neither, because their debt stays on their account.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var hasUnpaidCafe = false;
        if (attendance.IsGuest)
        {
            await db.LockAttendanceAsync(id, cancellationToken);
            hasUnpaidCafe = (await GuestCafe.UnpaidOrdersAsync(db, id, cancellationToken)).Count > 0;
        }

        // A cardio-only visit leaves only once its هوازی amount is recorded (BUSINESS_RULES.md §7
        // Cardio-only visit). The member's lock is the one voiding a charge takes, so a void cannot
        // land between this check and the close.
        var hasCardioCharge = false;
        if (attendance.IsCardioOnly)
        {
            await db.LockMemberAsync(attendance.MemberId!.Value, cancellationToken);
            hasCardioCharge = await db.ServiceCharges.AnyAsync(
                charge => charge.AttendanceId == id && charge.Kind == ServiceChargeKind.Cardio && charge.VoidedAt == null,
                cancellationToken);
        }

        var checkedOut = attendance.CheckOut(time.GetUtcNow(), hasUnpaidCafe, hasCardioCharge);
        if (checkedOut.IsFailure)
        {
            return Result.Failure<AttendanceResponse>(checkedOut.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Closed or moved by someone else since it was read (the visit's xmin changed).
            return Result.Failure<AttendanceResponse>(AttendanceErrors.ChangedConcurrently);
        }

        var lockerNumber = attendance.LockerId is null
            ? null
            : await db.Lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).SingleOrDefaultAsync(cancellationToken);

        // Read after the close, so the charges come back with CanChangeAmount already false:
        // after check-out the amount is corrected with a void, not edited (BUSINESS_RULES.md §7).
        var charges = await VisitServiceCharges.ByAttendanceAsync(db, [id], cancellationToken);

        return AttendanceResponse.From(attendance, lockerNumber, charges.GetValueOrDefault(id, []));
    }
}

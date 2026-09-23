using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges.VoidServiceCharge;

/// <summary>
/// Undoes a charge without erasing it (BUSINESS_RULES.md §7 <i>Gym services</i>, §5: financial
/// records are never deleted). The charge stops counting toward the member's debt, and anything
/// already collected for it is refunded in the same transaction.
/// </summary>
/// <remarks>
/// Staff or Owner, decided with the developer 1405/07/01. §1's permissions table puts "refunds,
/// voids" with the Owner, and this is a deliberate exception: a cardio amount is typed at the desk
/// and the desk has to be able to take back its own mistake while the member is still standing
/// there. The controls are that the reason is required and the audit log records who did it.
/// </remarks>
public sealed class VoidServiceChargeHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<ServiceChargeResponse>> Handle(
        Guid id, VoidServiceChargeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var charge = await db.ServiceCharges.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (charge is null)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.NotFound);
        }

        // The refund is written from what the payments add up to, so that sum is read under the
        // same per-member lock a payment takes — otherwise money arriving at this moment would
        // stay in the gym's books against a charge that owes nothing.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(charge.MemberId, cancellationToken);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Voiding a service charge was called without an authenticated user.");

        var now = time.GetUtcNow();

        var voided = charge.Void(command.Reason, now, userId);
        if (voided.IsFailure)
        {
            return Result.Failure<ServiceChargeResponse>(voided.Error);
        }

        await ServiceChargeRefunder.RefundNetPaidAsync(db, charge, charge.VoidReason!, userId, now, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.ChangedConcurrently);
        }

        var visitIsOpen = await db.Attendances.AsNoTracking()
            .AnyAsync(a => a.Id == charge.AttendanceId && a.CheckedOutAt == null, cancellationToken);

        // Net paid is zero by construction: whatever had been collected was just refunded.
        return ServiceChargeResponse.From(charge, netPaid: 0, visitIsOpen);
    }
}

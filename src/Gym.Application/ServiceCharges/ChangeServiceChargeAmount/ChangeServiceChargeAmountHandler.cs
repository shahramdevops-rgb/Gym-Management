using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges.ChangeServiceChargeAmount;

/// <summary>
/// Corrects a charge's amount while nothing has been settled: the visit is still open and no
/// money has been taken against it (BUSINESS_RULES.md §7 <i>Gym services</i>). Once either of
/// those stops being true it is a financial record, and the correction is a void plus a reason.
/// Front desk work, so both roles.
/// </summary>
public sealed class ChangeServiceChargeAmountHandler(IAppDbContext db)
{
    public async Task<Result<ServiceChargeResponse>> Handle(
        Guid id, ChangeServiceChargeAmountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var charge = await db.ServiceCharges.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (charge is null)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.NotFound);
        }

        // "Nothing has been paid against it" is a sum across the payments table, so it is read
        // under the same per-member lock that registering a payment takes. Above the lock it would
        // be a suggestion: a payment committing between the read and the save would leave a paid
        // charge showing an amount nobody agreed to.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(charge.MemberId, cancellationToken);

        var visitIsOpen = await db.Attendances.AsNoTracking()
            .AnyAsync(a => a.Id == charge.AttendanceId && a.CheckedOutAt == null, cancellationToken);
        if (!visitIsOpen)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.VisitNotOpen);
        }

        var netPaid = await PaymentLedger.GetNetPaidForServiceChargeAsync(db, id, cancellationToken);
        if (netPaid != 0)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.AlreadyPaid);
        }

        var changed = charge.ChangeAmount(command.Amount);
        if (changed.IsFailure)
        {
            return Result.Failure<ServiceChargeResponse>(changed.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.ChangedConcurrently);
        }

        return ServiceChargeResponse.From(charge, netPaid, visitIsOpen: true);
    }
}

using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges.RecordMiscellaneousSale;

/// <summary>
/// Sells something the desk names itself during an open visit (BUSINESS_RULES.md §7
/// <i>Miscellaneous sale</i>): paid there and then in full, or left on the member's account.
/// Front desk work, so both roles.
/// </summary>
/// <remarks>
/// The sale and its payment are saved together, so a sale the desk saw as paid can never be left
/// owed by a failure between two requests. No member lock is needed for the payment: nothing else
/// can pay a charge that does not exist yet, so the overpayment check has nothing to race with.
/// </remarks>
public sealed class RecordMiscellaneousSaleHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<ServiceChargeResponse>> Handle(
        Guid attendanceId, RecordMiscellaneousSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = await db.Attendances.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attendanceId, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<ServiceChargeResponse>(AttendanceErrors.NotFound);
        }

        // Only an open visit, the same as هوازی: a closed visit is history.
        if (attendance.CheckedOutAt is not null)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.VisitNotOpen);
        }

        // Members only (decided with the developer, 1405/07/11): a guest has no account to put it on.
        if (attendance.MemberId is not { } memberId)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.GuestVisit);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Recording a miscellaneous sale was called without an authenticated user.");

        var recorded = ServiceCharge.RecordMiscellaneous(
            memberId, attendanceId, command.Description, command.Quantity, command.UnitPrice, calendar.Today(), userId);
        if (recorded.IsFailure)
        {
            return Result.Failure<ServiceChargeResponse>(recorded.Error);
        }

        var charge = recorded.Value;
        db.ServiceCharges.Add(charge);

        var netPaid = 0m;
        if (command.Method is { } method)
        {
            var paid = Payment.RegisterForServiceCharge(
                charge.Id, charge.Amount, method, referenceNumber: null, userId, time.GetUtcNow());
            if (paid.IsFailure)
            {
                return Result.Failure<ServiceChargeResponse>(paid.Error);
            }

            db.Payments.Add(paid.Value);
            netPaid = charge.Amount;
        }

        await db.SaveChangesAsync(cancellationToken);

        return ServiceChargeResponse.From(charge, netPaid, visitIsOpen: true);
    }
}

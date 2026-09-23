using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.RegisterPayment;

/// <summary>
/// Registers a payment against a service charge, partial or in full (BUSINESS_RULES.md §5, §7:
/// "a service charge is paid like anything else"). Owner and Staff.
/// </summary>
/// <remarks>
/// There is no matching refund endpoint, and that is not an omission: a charge that needs
/// correcting is voided with a reason (§7), and the void gives back whatever was collected. A
/// partial refund of a treadmill amount is a void and a fresh, smaller charge.
/// </remarks>
public sealed class RegisterServiceChargePaymentHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PaymentResponse>> Handle(
        Guid serviceChargeId, RegisterPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var charge = await db.ServiceCharges.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == serviceChargeId, cancellationToken);
        if (charge is null)
        {
            return Result.Failure<PaymentResponse>(ServiceChargeErrors.NotFound);
        }

        // A voided charge owes nothing (§5 Member debt), so there is nothing to pay against it.
        if (charge.VoidedAt is not null)
        {
            return Result.Failure<PaymentResponse>(ServiceChargeErrors.AlreadyVoided);
        }

        // "Cannot be overpaid" is a sum-across-rows invariant no check constraint can express, so
        // two payments racing for the same charge are serialized by the member lock, exactly as
        // RegisterPaymentHandler does for a subscription.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(charge.MemberId, cancellationToken);

        var netPaid = await PaymentLedger.GetNetPaidForServiceChargeAsync(db, serviceChargeId, cancellationToken);
        var updatedNetPaid = netPaid + command.Amount;
        if (updatedNetPaid > charge.Amount)
        {
            return Result.Failure<PaymentResponse>(PaymentErrors.Overpayment);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Register payment was called without an authenticated user.");

        var registered = Payment.RegisterForServiceCharge(
            serviceChargeId, command.Amount, command.Method, command.ReferenceNumber, userId, time.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<PaymentResponse>(registered.Error);
        }

        db.Payments.Add(registered.Value);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var status = PaymentStatusCalculator.Calculate(charge.Amount, updatedNetPaid);

        return PaymentResponse.From(registered.Value, updatedNetPaid, status);
    }
}

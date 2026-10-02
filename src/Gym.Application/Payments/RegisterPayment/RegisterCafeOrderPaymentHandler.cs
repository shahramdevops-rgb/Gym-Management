using Gym.Application.Common;
using Gym.Domain.Cafe;
using Gym.Domain.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.RegisterPayment;

/// <summary>
/// Registers a payment against a cafe order left on a member's account or on a guest's visit, partial or in full
/// (BUSINESS_RULES.md §5, §8: "settled later with ordinary Payment rows"). Owner and Staff.
/// </summary>
/// <remarks>
/// A walk-in order never reaches here: it is paid in full when it is created, because it names no
/// member and there is no account to settle against. An order that is already fully paid is
/// refused by the overpayment guard below, not by a status column.
/// </remarks>
public sealed class RegisterCafeOrderPaymentHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PaymentResponse>> Handle(
        Guid cafeOrderId, RegisterPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await db.CafeOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == cafeOrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<PaymentResponse>(CafeOrderErrors.NotFound);
        }

        // "Cannot be overpaid" is a sum-across-rows invariant no check constraint can express, so
        // two payments racing for the same order are serialized the same way the other two payment
        // handlers do it. A walk-in order has no member row to lock, but it also cannot be paid
        // here: it was settled in full at creation, inside its own transaction. An order on a
        // guest's visit locks the visit instead, the lock the guest's check-out also takes.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (order.MemberId is { } memberId)
        {
            await db.LockMemberAsync(memberId, cancellationToken);
        }
        else if (order.AttendanceId is { } guestVisitId)
        {
            await db.LockAttendanceAsync(guestVisitId, cancellationToken);
        }

        // A cancelled order owes nothing (§5 Member debt), so there is nothing to pay against it.
        // Asked again under the lock rather than trusted from the read above: cancelling takes the
        // same lock, so a cancellation that finished while this request waited is seen here.
        var isCancelled = await db.CafeOrders
            .AnyAsync(o => o.Id == cafeOrderId && o.CancelledAt != null, cancellationToken);
        if (isCancelled)
        {
            return Result.Failure<PaymentResponse>(CafeOrderErrors.AlreadyCancelled);
        }

        var netPaid = await PaymentLedger.GetNetPaidForCafeOrderAsync(db, cafeOrderId, cancellationToken);
        var updatedNetPaid = netPaid + command.Amount;
        if (updatedNetPaid > order.TotalAmount)
        {
            return Result.Failure<PaymentResponse>(PaymentErrors.Overpayment);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Register payment was called without an authenticated user.");

        var registered = Payment.RegisterForCafeOrder(
            cafeOrderId, command.Amount, command.Method, command.ReferenceNumber, userId, time.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<PaymentResponse>(registered.Error);
        }

        db.Payments.Add(registered.Value);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var status = PaymentStatusCalculator.Calculate(order.TotalAmount, updatedNetPaid);

        return PaymentResponse.From(registered.Value, updatedNetPaid, status);
    }
}

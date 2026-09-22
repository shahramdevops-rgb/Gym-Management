using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.RegisterRefund;

/// <summary>
/// Refunds a subscription, partial or a full "void" of a mistaken payment (BUSINESS_RULES.md §5).
/// Owner only.
/// </summary>
public sealed class RegisterRefundHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PaymentResponse>> Handle(
        Guid subscriptionId, RegisterRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await db.Subscriptions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PaymentResponse>(SubscriptionErrors.NotFound);
        }

        // The mirror image of RegisterPaymentHandler's overpayment guard: "cannot exceed net
        // paid" is a sum-across-rows invariant, so the same per-member lock serializes a refund
        // racing with another payment or refund for the same subscription.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(subscription.MemberId, cancellationToken);

        // BUSINESS_RULES.md §5: sessions already taken are not bought back. Read again under the
        // lock rather than from the copy above, because check-in takes this same member lock
        // before it consumes a session — otherwise a visit starting now could slip past the check.
        var usedSessions = await db.Subscriptions.AsNoTracking()
            .Where(s => s.Id == subscriptionId)
            .Select(s => s.UsedSessions)
            .SingleAsync(cancellationToken);
        if (usedSessions > 0)
        {
            return Result.Failure<PaymentResponse>(PaymentErrors.RefundAfterUse);
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, subscriptionId, cancellationToken);
        if (command.Amount > netPaid)
        {
            return Result.Failure<PaymentResponse>(PaymentErrors.RefundExceedsNetPaid);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Register refund was called without an authenticated user.");

        var registered = Payment.RegisterRefundForSubscription(
            subscriptionId, command.Amount, command.Method, command.ReferenceNumber, command.Reason, userId, time.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<PaymentResponse>(registered.Error);
        }

        db.Payments.Add(registered.Value);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedNetPaid = netPaid - command.Amount;
        var status = PaymentStatusCalculator.Calculate(subscription.Price, updatedNetPaid);

        return PaymentResponse.From(registered.Value, updatedNetPaid, status);
    }
}

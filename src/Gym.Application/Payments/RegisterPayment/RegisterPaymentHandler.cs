using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payments;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payments.RegisterPayment;

/// <summary>
/// Registers a payment against a subscription, partial or in full (BUSINESS_RULES.md §5).
/// Owner and Staff.
/// </summary>
public sealed class RegisterPaymentHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<PaymentResponse>> Handle(
        Guid subscriptionId, RegisterPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await db.Subscriptions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PaymentResponse>(SubscriptionErrors.NotFound);
        }

        // "A subscription cannot be overpaid" (§5) is a sum-across-rows invariant no single check
        // constraint can express, so two payments racing for the same subscription are serialized
        // the same way two sales for the same member are (SubscriptionSeller): the second waits
        // for the first to commit, then reads the up-to-date net paid amount.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(subscription.MemberId, cancellationToken);

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, subscriptionId, cancellationToken);
        var updatedNetPaid = netPaid + command.Amount;
        if (updatedNetPaid > subscription.Price)
        {
            return Result.Failure<PaymentResponse>(PaymentErrors.Overpayment);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Register payment was called without an authenticated user.");

        var registered = Payment.RegisterForSubscription(
            subscriptionId, command.Amount, command.Method, command.ReferenceNumber, userId, time.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<PaymentResponse>(registered.Error);
        }

        db.Payments.Add(registered.Value);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var status = PaymentStatusCalculator.Calculate(subscription.Price, updatedNetPaid);

        return PaymentResponse.From(registered.Value, updatedNetPaid, status);
    }
}

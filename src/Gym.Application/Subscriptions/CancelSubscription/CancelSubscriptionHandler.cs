using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.CancelSubscription;

/// <summary>
/// Cancels a subscription nobody has used yet and gives back everything paid on it, in one
/// transaction (BUSINESS_RULES.md §4 Cancel, roadmap 6.5.37). Owner only. Queued subscriptions are
/// not moved earlier.
/// </summary>
/// <remarks>
/// Before 6.5.37 the payments were left alone and the refund was a second step, which at the gym
/// was once forgotten: the money stayed against a subscription that owed nothing.
/// </remarks>
public sealed class CancelSubscriptionHandler(
    IAppDbContext db, IGymCalendar calendar, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        // The refunds are written from what the payments add up to, so that sum is read under the
        // per-member lock a payment takes: a payment arriving at this moment either lands first
        // and is refunded too, or waits and then finds the subscription cancelled. A check-in
        // takes the same lock, and changing UsedSessions changes the row's xmin, so a session
        // consumed since the read above fails the save and rolls the refunds back with it.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(subscription.MemberId, cancellationToken);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cancelling a subscription was called without an authenticated user.");

        var today = calendar.Today();
        var now = time.GetUtcNow();

        var cancelled = subscription.Cancel(command.Reason, today, now);
        if (cancelled.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(cancelled.Error);
        }

        await SubscriptionRefunder.RefundNetPaidAsync(db, subscription, userId, now, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        // Net paid is zero by construction: whatever had been collected was just refunded.
        return SubscriptionResponse.From(subscription, today, netPaid: 0);
    }
}

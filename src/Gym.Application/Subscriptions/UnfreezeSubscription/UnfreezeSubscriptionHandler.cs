using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.UnfreezeSubscription;

/// <summary>
/// Ends a freeze, extends <see cref="Subscription.EndDate"/> by the frozen days, and shifts the
/// member's queued subscriptions by the same amount (BUSINESS_RULES.md §4 Freeze, task 4.3).
/// Owner only.
/// </summary>
public sealed class UnfreezeSubscriptionHandler(IAppDbContext db, IGymCalendar calendar, ISubscriptionPolicy policy)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var found = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (found is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        var today = calendar.Today();

        // Extending EndDate and shifting the queued subscriptions behind it moves several rows
        // at once and must pass through a moment where their date ranges overlap the old ones,
        // so both the member lock and the deferred exclusion check are needed here, unlike
        // Freeze and Cancel which touch one row and never move a date.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(found.MemberId, cancellationToken);
        await db.DeferSubscriptionOverlapCheckAsync(cancellationToken);

        // Tracked, all of them: the schedule picks which ones are queued behind this one and moves
        // them, and they are saved together. Check-in unfreezes by the same rule (roadmap 6.5.9).
        var memberSubscriptions = await db.Subscriptions
            .Where(s => s.MemberId == found.MemberId)
            .ToListAsync(cancellationToken);
        var subscription = memberSubscriptions.Single(s => s.Id == id);

        var unfrozen = SubscriptionSchedule.Unfreeze(subscription, today, policy.MaxFreezeDays, memberSubscriptions);
        if (unfrozen.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(unfrozen.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await db.CommitTransactionAsync(transaction, cancellationToken);
        }
        catch (ExclusionConstraintException exception) when (exception.ConstraintName == SubscriptionConstraints.NoOverlap)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, id, cancellationToken);

        return SubscriptionResponse.From(subscription, today, netPaid);
    }
}

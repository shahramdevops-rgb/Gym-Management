using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions;

/// <summary>
/// The part assign and renew share: place a new subscription in the member's calendar and save
/// it (BUSINESS_RULES.md §4). The handlers decide which member and plan; this decides when.
/// </summary>
public sealed class SubscriptionSeller(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SubscriptionResponse>> SellAsync(Member member, Plan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(plan);

        var canReceive = member.EnsureCanReceiveSubscription();
        if (canReceive.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(canReceive.Error);
        }

        var today = calendar.Today();

        // One sale per member at a time: a second sale for the same member waits here until the
        // first commits, then reads its subscription and queues after it. Without the lock both
        // would read the same calendar and pick the same start date.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(member.Id, cancellationToken);

        // Only subscriptions that still cover today or later decide the start date; ones that
        // ended before today or were cancelled do not. Tracked, because an exhausted one may be
        // closed early and must be saved together with the new one.
        var currentOrQueued = await db.Subscriptions
            .Where(s => s.MemberId == member.Id && s.CancelledAt == null && s.EndDate >= today)
            .ToListAsync(cancellationToken);

        var startDate = SubscriptionSchedule.NextStartDate(today, currentOrQueued);

        var created = Subscription.Create(member.Id, plan, startDate);
        if (created.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(created.Error);
        }

        db.Subscriptions.Add(created.Value);

        // The lock makes these unreachable in normal use. They remain for a writer that does not
        // take the lock: the exclusion constraint still refuses the overlap, and xmin a second
        // change to the same subscription.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ExclusionConstraintException exception) when (exception.ConstraintName == SubscriptionConstraints.NoOverlap)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        // A subscription just sold cannot have a payment against it yet.
        return SubscriptionResponse.From(created.Value, today, netPaid: 0m);
    }
}

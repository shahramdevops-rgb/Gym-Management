using Gym.Application.Common;
using Gym.Application.Pricing;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gym.Application.Subscriptions;

/// <summary>
/// The part every sale shares: price it from today's price list, place it in the member's calendar
/// and save it (BUSINESS_RULES.md §3, §4). The handlers decide which member and which numbers; this
/// decides when and at what price.
/// </summary>
/// <remarks>
/// Each sale comes in two forms. <c>Sell…Async</c> is a sale on its own: it opens the transaction,
/// takes the member's lock and commits. <c>Add…Async</c> is the same sale inside a transaction the
/// caller already holds, for check-in, which sells and lets the member in as one operation
/// (BUSINESS_RULES.md §7 <i>Confirming at the front desk</i>, roadmap 6.5.7).
/// </remarks>
public sealed class SubscriptionSeller(IAppDbContext db, IGymCalendar calendar)
{
    /// <summary>A plan of so many sessions, for the days they give: assign and renew.</summary>
    public async Task<Result<SubscriptionResponse>> SellMembershipAsync(
        Member member, int sessionCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        // One sale per member at a time: a second sale for the same member waits here until the
        // first commits, then reads its subscription and queues after it. Without the lock both
        // would read the same calendar and pick the same start date.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(member.Id, cancellationToken);

        var today = calendar.Today();
        var added = await AddMembershipAsync(member, sessionCount, today, cancellationToken);
        if (added.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(added.Error);
        }

        return await SaveAsync(added.Value, transaction, today, cancellationToken);
    }

    /// <summary>
    /// One visit for today (BUSINESS_RULES.md §4 <i>Single-session subscriptions</i>). It reads no
    /// calendar and moves nothing, but it still takes the member's lock, so it cannot land between a
    /// membership sale's read and its write.
    /// </summary>
    public async Task<Result<SubscriptionResponse>> SellSingleVisitAsync(
        Member member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockMemberAsync(member.Id, cancellationToken);

        var today = calendar.Today();
        var added = await AddSingleVisitAsync(member, today, cancellationToken);
        if (added.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(added.Error);
        }

        return await SaveAsync(added.Value, transaction, today, cancellationToken);
    }

    /// <summary>
    /// Prices and places a plan and adds it to the context, unsaved. The caller must already hold
    /// the member's lock inside its own transaction, and saves.
    /// </summary>
    public async Task<Result<Subscription>> AddMembershipAsync(
        Member member, int sessionCount, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        var canReceive = member.EnsureCanReceiveSubscription();
        if (canReceive.IsFailure)
        {
            return Result.Failure<Subscription>(canReceive.Error);
        }

        var prices = await PriceLists.CurrentAsync(db, cancellationToken);

        // Only subscriptions that still cover today or later decide the start date; ones that
        // ended before today or were cancelled do not. Single-session ones never do either
        // (BUSINESS_RULES.md §4): a member who dropped in today would otherwise have the plan they
        // buy an hour later start tomorrow. Tracked, because an exhausted one may be closed early
        // and must be saved together with the new one.
        var currentOrQueued = await db.Subscriptions
            .Where(s => s.MemberId == member.Id && s.CancelledAt == null && s.EndDate >= today
                        && !s.IsSingleSession)
            .ToListAsync(cancellationToken);

        var startDate = SubscriptionSchedule.NextStartDate(today, currentOrQueued);

        var created = Subscription.CreateMembership(member.Id, sessionCount, prices.SessionPrice, startDate);
        if (created.IsFailure)
        {
            return created;
        }

        db.Subscriptions.Add(created.Value);

        return created;
    }

    /// <summary>
    /// Prices a single visit for today and adds it to the context, unsaved. The caller must already
    /// hold the member's lock inside its own transaction, and saves.
    /// </summary>
    public async Task<Result<Subscription>> AddSingleVisitAsync(
        Member member, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        var canReceive = member.EnsureCanReceiveSubscription();
        if (canReceive.IsFailure)
        {
            return Result.Failure<Subscription>(canReceive.Error);
        }

        var prices = await PriceLists.CurrentAsync(db, cancellationToken);

        var created = Subscription.CreateSingleVisit(member.Id, prices.SingleVisitPrice, today);
        if (created.IsFailure)
        {
            return created;
        }

        db.Subscriptions.Add(created.Value);

        return created;
    }

    private async Task<Result<SubscriptionResponse>> SaveAsync(
        Subscription subscription,
        IDbContextTransaction transaction,
        DateOnly today,
        CancellationToken cancellationToken)
    {
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
        return SubscriptionResponse.From(subscription, today, netPaid: 0m);
    }
}

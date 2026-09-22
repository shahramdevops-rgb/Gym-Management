using Gym.Application.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members;

/// <summary>
/// What a member still owes (BUSINESS_RULES.md §5 <i>Member debt</i>), in one place so the member
/// list, the profile's breakdown and the check-in warning all answer the same number — the way
/// <see cref="Payments.PaymentLedger"/> owns "net paid".
/// </summary>
/// <remarks>
/// <para>
/// Debt is <b>calculated, never stored</b>: there is no balance column to keep correct and no
/// nightly job to recompute one. Per item it is <c>Price − net paid</c>, never below zero, summed
/// over the member's non-cancelled subscriptions. Cancelling is how the gym says it is not chasing
/// that money, so a cancelled subscription owes nothing even if it was never paid.
/// </para>
/// <para>
/// Service charges (roadmap 5.7) and cafe orders (Phase 7) join the same total when they exist.
/// </para>
/// </remarks>
public static class MemberDebt
{
    /// <summary>One owed item of the breakdown: what it is, its dates, and what is left on it.</summary>
    public sealed record Item(
        Guid SubscriptionId,
        Guid PlanId,
        DateOnly StartDate,
        DateOnly EndDate,
        decimal Price,
        decimal NetPaid,
        decimal Outstanding);

    /// <summary>
    /// The member's outstanding items, newest first. A fully paid or free item owes nothing and is
    /// left out: the breakdown answers "what is still owed", not "what was ever sold".
    /// </summary>
    public static async Task<List<Item>> GetItemsAsync(
        IAppDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(subscription => subscription.MemberId == memberId && subscription.CancelledAt == null)
            .OrderByDescending(subscription => subscription.StartDate)
            .ThenByDescending(subscription => subscription.Id)
            .Select(subscription => new
            {
                subscription.Id,
                subscription.PlanId,
                subscription.StartDate,
                subscription.EndDate,
                subscription.Price,
                NetPaid = db.Payments
                    .Where(payment => payment.SubscriptionId == subscription.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToListAsync(cancellationToken);

        return subscriptions
            .Select(subscription => new Item(
                subscription.Id,
                subscription.PlanId,
                subscription.StartDate,
                subscription.EndDate,
                subscription.Price,
                subscription.NetPaid,
                Outstanding(subscription.Price, subscription.NetPaid)))
            .Where(item => item.Outstanding > 0)
            .ToList();
    }

    /// <summary>The member's total, the sum of <see cref="GetItemsAsync"/>'s outstanding amounts.</summary>
    public static async Task<decimal> GetTotalAsync(
        IAppDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        var items = await GetItemsAsync(db, memberId, cancellationToken);

        return items.Sum(item => item.Outstanding);
    }

    /// <summary>
    /// One total per member, for a page of the member list. Two queries for the whole page rather
    /// than two per member, and members who owe nothing are simply absent from the dictionary.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> GetTotalsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(memberIds);

        if (memberIds.Count == 0)
        {
            return [];
        }

        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(subscription => memberIds.Contains(subscription.MemberId) && subscription.CancelledAt == null)
            .Select(subscription => new { subscription.Id, subscription.MemberId, subscription.Price })
            .ToListAsync(cancellationToken);

        var subscriptionIds = subscriptions.Select(subscription => subscription.Id).ToList();

        var netPaidById = await db.Payments.AsNoTracking()
            .Where(payment => payment.SubscriptionId != null && subscriptionIds.Contains(payment.SubscriptionId!.Value))
            .GroupBy(payment => payment.SubscriptionId!.Value)
            .Select(group => new
            {
                SubscriptionId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.SubscriptionId, row => row.NetPaid, cancellationToken);

        return subscriptions
            .GroupBy(subscription => subscription.MemberId)
            .Select(group => new
            {
                MemberId = group.Key,
                Total = group.Sum(subscription =>
                    Outstanding(subscription.Price, netPaidById.GetValueOrDefault(subscription.Id))),
            })
            .Where(row => row.Total > 0)
            .ToDictionary(row => row.MemberId, row => row.Total);
    }

    /// <summary>
    /// Never below zero: a refund cannot exceed net paid and a payment cannot exceed the price
    /// (§5), so this only guards against an item that owes nothing counting as negative debt and
    /// cancelling out another item the member really does owe.
    /// </summary>
    private static decimal Outstanding(decimal price, decimal netPaid) => Math.Max(0, price - netPaid);
}

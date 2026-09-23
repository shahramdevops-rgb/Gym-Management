using Gym.Application.Common;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

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
/// over the member's non-cancelled subscriptions and non-voided service charges. Cancelling or
/// voiding is how the gym says it is not chasing that money, so such an item owes nothing even if
/// it was never paid.
/// </para>
/// <para>
/// Cafe orders (Phase 7) join the same total when they exist.
/// </para>
/// </remarks>
public static class MemberDebt
{
    /// <summary>
    /// One owed item of the breakdown: what it is, its dates, and what is left on it. The fields
    /// only one kind has are nullable rather than split into two types, because every caller shows
    /// them as one list ordered by date.
    /// </summary>
    /// <param name="Id">The subscription's or the service charge's id — what a payment is posted against.</param>
    /// <param name="PlanId"><c>null</c> for a service charge.</param>
    /// <param name="ServiceKind"><c>null</c> for a subscription.</param>
    /// <param name="StartDate">The subscription's start date, or the day of the visit that was charged.</param>
    /// <param name="EndDate"><c>null</c> for a service charge: it covers the one day it was charged on.</param>
    public sealed record Item(
        PaymentTargetKind Kind,
        Guid Id,
        Guid? PlanId,
        ServiceChargeKind? ServiceKind,
        DateOnly StartDate,
        DateOnly? EndDate,
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

        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => charge.MemberId == memberId && charge.VoidedAt == null)
            .Select(charge => new
            {
                charge.Id,
                charge.Kind,
                charge.ChargedOn,
                charge.Amount,
                NetPaid = db.Payments
                    .Where(payment => payment.ServiceChargeId == charge.Id)
                    .Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToListAsync(cancellationToken);

        var items = subscriptions
            .Select(subscription => new Item(
                PaymentTargetKind.Subscription,
                subscription.Id,
                subscription.PlanId,
                ServiceKind: null,
                subscription.StartDate,
                subscription.EndDate,
                subscription.Price,
                subscription.NetPaid,
                Outstanding(subscription.Price, subscription.NetPaid)))
            .Concat(charges.Select(charge => new Item(
                PaymentTargetKind.ServiceCharge,
                charge.Id,
                PlanId: null,
                charge.Kind,
                charge.ChargedOn,
                EndDate: null,
                charge.Amount,
                charge.NetPaid,
                Outstanding(charge.Amount, charge.NetPaid))));

        // Sorted here rather than in each query, because the two kinds are one list to the reader.
        return items
            .Where(item => item.Outstanding > 0)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .ToList();
    }

    /// <summary>The member's total, the sum of <see cref="GetItemsAsync"/> outstanding amounts.</summary>
    public static async Task<decimal> GetTotalAsync(
        IAppDbContext db, Guid memberId, CancellationToken cancellationToken)
    {
        var items = await GetItemsAsync(db, memberId, cancellationToken);

        return items.Sum(item => item.Outstanding);
    }

    /// <summary>
    /// One total per member, for a page of the member list. A fixed number of queries for the
    /// whole page rather than a pair per member, and members who owe nothing are simply absent
    /// from the dictionary.
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

        var subscriptionNetPaid = await db.Payments.AsNoTracking()
            .Where(payment => payment.SubscriptionId != null && subscriptionIds.Contains(payment.SubscriptionId!.Value))
            .GroupBy(payment => payment.SubscriptionId!.Value)
            .Select(group => new
            {
                TargetId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.TargetId, row => row.NetPaid, cancellationToken);

        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => memberIds.Contains(charge.MemberId) && charge.VoidedAt == null)
            .Select(charge => new { charge.Id, charge.MemberId, Price = charge.Amount })
            .ToListAsync(cancellationToken);

        var chargeIds = charges.Select(charge => charge.Id).ToList();

        var chargeNetPaid = await db.Payments.AsNoTracking()
            .Where(payment => payment.ServiceChargeId != null && chargeIds.Contains(payment.ServiceChargeId!.Value))
            .GroupBy(payment => payment.ServiceChargeId!.Value)
            .Select(group => new
            {
                TargetId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.TargetId, row => row.NetPaid, cancellationToken);

        var owed = subscriptions
            .Select(subscription => new
            {
                subscription.MemberId,
                Outstanding = Outstanding(subscription.Price, subscriptionNetPaid.GetValueOrDefault(subscription.Id)),
            })
            .Concat(charges.Select(charge => new
            {
                charge.MemberId,
                Outstanding = Outstanding(charge.Price, chargeNetPaid.GetValueOrDefault(charge.Id)),
            }));

        return owed
            .GroupBy(row => row.MemberId)
            .Select(group => new { MemberId = group.Key, Total = group.Sum(row => row.Outstanding) })
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

using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions;

/// <summary>
/// A sold subscription shows the plan's name as it reads <i>now</i>, not as it read on the day
/// of the sale (BUSINESS_RULES.md §4), so every feature that displays one looks the name up
/// through <c>PlanId</c> instead of reading a stored copy. In one place, the way
/// <see cref="Gym.Application.Payments.PaymentLedger"/> owns "net paid".
/// </summary>
/// <remarks>
/// The lookup always succeeds: <c>Subscription.PlanId</c> is required, its foreign key is
/// <c>ON DELETE RESTRICT</c>, and plans are deactivated rather than deleted — a sale is a
/// financial record and the plan behind it has to stay.
/// </remarks>
public static class PlanNames
{
    public static async Task<string> ForAsync(IAppDbContext db, Guid planId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return await db.Plans
            .Where(plan => plan.Id == planId)
            .Select(plan => plan.Name)
            .SingleAsync(cancellationToken);
    }

    /// <summary>The names for a page of subscriptions in one query, keyed by plan id.</summary>
    public static async Task<Dictionary<Guid, string>> ByIdAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(planIds);

        if (planIds.Count == 0)
        {
            return [];
        }

        return await db.Plans
            .Where(plan => planIds.Contains(plan.Id))
            .ToDictionaryAsync(plan => plan.Id, plan => plan.Name, cancellationToken);
    }
}

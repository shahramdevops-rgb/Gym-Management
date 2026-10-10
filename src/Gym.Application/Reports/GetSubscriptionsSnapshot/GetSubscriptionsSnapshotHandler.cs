using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetSubscriptionsSnapshot;

/// <summary>
/// How many plans are active, frozen and running out today (BUSINESS_RULES.md §12 <i>Operational
/// reports</i>, roadmap 9.2). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// The status is calculated, never stored (§4), so the conditions of <c>Subscription.GetStatus</c>
/// are written out here as SQL the database can count: not cancelled, not frozen, started, not
/// ended, a session left. A plan that is running out is still counted as active; these figures
/// describe plans, and whether the member has already renewed is the «نیاز به اقدام» lists'
/// question, not this one's.
/// </remarks>
public sealed class GetSubscriptionsSnapshotHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<SubscriptionsSnapshotResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var expiringBy = today.AddDays(ReportThresholds.ExpiringWithinDays);

        // Grouping on a constant turns the counts into one SELECT; no plan at all means no group.
        var counts = await db.Subscriptions
            .AsNoTracking()
            .Where(subscription => !subscription.IsSingleSession && subscription.CancelledAt == null)
            .GroupBy(subscription => 1)
            .Select(plans => new
            {
                Active = plans.Count(plan => plan.FrozenSince == null && plan.StartDate <= today &&
                    plan.EndDate >= today && plan.UsedSessions < plan.TotalSessions),
                Frozen = plans.Count(plan => plan.FrozenSince != null),
                ExpiringSoon = plans.Count(plan => plan.FrozenSince == null && plan.StartDate <= today &&
                    plan.EndDate >= today && plan.UsedSessions < plan.TotalSessions && plan.EndDate <= expiringBy),
                LowSessions = plans.Count(plan => plan.FrozenSince == null && plan.StartDate <= today &&
                    plan.EndDate >= today && plan.UsedSessions < plan.TotalSessions &&
                    plan.TotalSessions - plan.UsedSessions <= ReportThresholds.LowSessions),
            })
            // Single, not First: there is one group or none, and EF warns about a First with no order
            // (task 11.3, production logs).
            .SingleOrDefaultAsync(cancellationToken);

        return counts is null
            ? new SubscriptionsSnapshotResponse(today, 0, 0, 0, 0)
            : new SubscriptionsSnapshotResponse(today, counts.Active, counts.Frozen, counts.ExpiringSoon, counts.LowSessions);
    }
}

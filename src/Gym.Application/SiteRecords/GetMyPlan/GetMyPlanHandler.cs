using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Application.Subscriptions;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteRecords.GetMyPlan;

/// <summary>
/// The member's plan now and the one bought for later (BUSINESS_RULES.md §15). The panel's card
/// picks its plan in the browser from the history; the site asks the domain's own rule instead,
/// the one the member list uses, so the app does not have to repeat it.
/// </summary>
public sealed class GetMyPlanHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MyPlanResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyPlanResponse>(member.Error);
        }

        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(subscription => subscription.MemberId == member.Value.Id)
            .ToListAsync(cancellationToken);

        var today = calendar.Today();
        var current = SubscriptionSchedule.CurrentPlan(today, subscriptions);
        var next = subscriptions
            .Where(subscription => subscription.CancelledAt is null
                && !subscription.IsSingleSession
                && subscription.Id != current?.Id
                && subscription.GetStatus(today) == SubscriptionStatus.Upcoming)
            .MinBy(subscription => subscription.StartDate);

        return new MyPlanResponse(
            await PlanAsync(current, today, cancellationToken),
            await PlanAsync(next, today, cancellationToken));
    }

    private async Task<SitePlanResponse?> PlanAsync(Subscription? subscription, DateOnly today, CancellationToken cancellationToken)
    {
        if (subscription is null)
        {
            return null;
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, subscription.Id, cancellationToken);

        return SitePlanResponse.From(SubscriptionResponse.From(subscription, today, netPaid));
    }
}

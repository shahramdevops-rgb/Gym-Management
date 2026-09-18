using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.RenewSubscription;

/// <summary>
/// Sells the member the same plan as their latest subscription, at the plan's current values
/// (decided in task 4.2). A one-click shortcut for assign; the start date follows the same rule.
/// </summary>
public sealed class RenewSubscriptionHandler(IAppDbContext db, SubscriptionSeller seller)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure<SubscriptionResponse>(MemberErrors.NotFound);
        }

        // Checked before looking for a plan, so an inactive member hears about that first.
        var canReceive = member.EnsureCanReceiveSubscription();
        if (canReceive.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(canReceive.Error);
        }

        // "Latest" by end date, cancelled ones included: a member who cancelled and came back
        // usually wants the same plan again. CreatedAt breaks a tie.
        var latestPlanId = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.EndDate)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => (Guid?)s.PlanId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestPlanId is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NothingToRenew);
        }

        // Plans are never deleted and the foreign key forbids it, so the plan is there.
        var plan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == latestPlanId, cancellationToken);

        return await seller.SellAsync(member, plan, cancellationToken);
    }
}

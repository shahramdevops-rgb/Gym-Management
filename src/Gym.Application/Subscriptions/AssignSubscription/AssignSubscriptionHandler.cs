using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>
/// Sells a chosen plan to a member, starting today or queued after their latest subscription
/// (BUSINESS_RULES.md §4). Owner and Staff.
/// </summary>
public sealed class AssignSubscriptionHandler(IAppDbContext db, SubscriptionSeller seller)
{
    public async Task<Result<SubscriptionResponse>> Handle(
        Guid memberId, AssignSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure<SubscriptionResponse>(MemberErrors.NotFound);
        }

        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == command.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<SubscriptionResponse>(PlanErrors.NotFound);
        }

        return await seller.SellAsync(member, plan, cancellationToken);
    }
}

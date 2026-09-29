using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.RenewSubscription;

/// <summary>
/// Sells the member the same sessions as their latest subscription, for the days they give today
/// and at today's session price (BUSINESS_RULES.md §4, rewritten in tasks 6.5.6 and 6.5.18). A
/// one-click shortcut for assign; the start date follows the same rule.
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

        // Checked before looking for a subscription, so an inactive member hears about that first.
        var canReceive = member.EnsureCanReceiveSubscription();
        if (canReceive.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(canReceive.Error);
        }

        // "Latest" by end date, cancelled ones included: a member who cancelled and came back
        // usually wants the same plan again. CreatedAt breaks a tie.
        //
        // Single-session sales are skipped (BUSINESS_RULES.md §4): a member who dropped in yesterday
        // still wants their plan renewed, not another single visit. A member whose only history is
        // single visits has nothing to renew.
        var latest = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId && !s.IsSingleSession)
            .OrderByDescending(s => s.EndDate)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => (int?)s.TotalSessions)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not { } sessionCount)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NothingToRenew);
        }

        // Only the sessions are reused, never the old price or days: the renewal costs what the plan
        // costs today and lasts what the table gives today.
        return await seller.SellMembershipAsync(member, sessionCount, cancellationToken);
    }
}

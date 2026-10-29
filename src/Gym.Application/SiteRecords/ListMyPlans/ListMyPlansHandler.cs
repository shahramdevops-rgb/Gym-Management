using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions.ListMemberSubscriptions;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.ListMyPlans;

/// <summary>The member's own plans, newest first, through the panel's history (BUSINESS_RULES.md §15).</summary>
public sealed class ListMyPlansHandler(IAppDbContext db, ListMemberSubscriptionsHandler plans)
{
    public async Task<Result<PagedResponse<SitePlanResponse>>> Handle(
        Guid siteAccountId, ListMemberSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<PagedResponse<SitePlanResponse>>(member.Error);
        }

        var page = await plans.Handle(member.Value.Id, query, cancellationToken);

        return page.IsFailure
            ? Result.Failure<PagedResponse<SitePlanResponse>>(page.Error)
            : SitePages.Map(page.Value, SitePlanResponse.From);
    }
}

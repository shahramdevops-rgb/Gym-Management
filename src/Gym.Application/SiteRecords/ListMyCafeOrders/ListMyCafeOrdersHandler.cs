using Gym.Application.Cafe.ListMemberCafeOrders;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.ListMyCafeOrders;

/// <summary>The member's own cafe purchases, newest first, through the panel's history (BUSINESS_RULES.md §15).</summary>
public sealed class ListMyCafeOrdersHandler(IAppDbContext db, ListMemberCafeOrdersHandler orders)
{
    public async Task<Result<PagedResponse<MyCafeOrderResponse>>> Handle(
        Guid siteAccountId, ListMemberCafeOrdersQuery query, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<PagedResponse<MyCafeOrderResponse>>(member.Error);
        }

        var page = await orders.Handle(member.Value.Id, query, cancellationToken);

        return page.IsFailure
            ? Result.Failure<PagedResponse<MyCafeOrderResponse>>(page.Error)
            : SitePages.Map(page.Value, MyCafeOrderResponse.From);
    }
}

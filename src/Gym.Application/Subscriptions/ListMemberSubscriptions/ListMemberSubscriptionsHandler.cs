using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.ListMemberSubscriptions;

/// <summary>A member's subscription history, newest first (BUSINESS_RULES.md §4, task 4.5).</summary>
public sealed class ListMemberSubscriptionsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<PagedResponse<SubscriptionResponse>>> Handle(
        Guid memberId, ListMemberSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberExists = await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            return Result.Failure<PagedResponse<SubscriptionResponse>>(MemberErrors.NotFound);
        }

        var subscriptions = db.Subscriptions.AsNoTracking().Where(s => s.MemberId == memberId);

        var totalCount = await subscriptions.CountAsync(cancellationToken);

        // The status is calculated by the entity (Subscription.GetStatus), so rows are loaded
        // rather than projected. Newest first: a queued or just-sold subscription is what the
        // front desk asks about, not one from a year ago.
        var today = calendar.Today();
        var items = await subscriptions
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var responses = items.Select(s => SubscriptionResponse.From(s, today)).ToList();

        return new PagedResponse<SubscriptionResponse>(responses, query.Page, query.PageSize, totalCount);
    }
}

using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

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
        //
        // Two subscriptions can share a StartDate: a cancelled subscription covers no dates
        // (BUSINESS_RULES.md §4), so renewing the same day it was cancelled starts the new one
        // on that same date. Id (a version 7 GUID, time-ordered) breaks the tie by creation
        // order, descending, so the new one — not the cancelled one — sorts first and is what
        // "the current subscription" (task 4.6, page 1 row 1) actually shows.
        var today = calendar.Today();
        var items = await subscriptions
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // One grouped query for the whole page instead of one per row (N+1).
        var subscriptionIds = items.Select(s => s.Id).ToList();
        var netPaidById = await db.Payments.AsNoTracking()
            .Where(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId!.Value))
            .GroupBy(p => p.SubscriptionId!.Value)
            .Select(group => new
            {
                SubscriptionId = group.Key,
                NetPaid = group.Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount),
            })
            .ToDictionaryAsync(row => row.SubscriptionId, row => row.NetPaid, cancellationToken);

        var responses = items
            .Select(s => SubscriptionResponse.From(s, today, netPaidById.GetValueOrDefault(s.Id)))
            .ToList();

        return new PagedResponse<SubscriptionResponse>(responses, query.Page, query.PageSize, totalCount);
    }
}

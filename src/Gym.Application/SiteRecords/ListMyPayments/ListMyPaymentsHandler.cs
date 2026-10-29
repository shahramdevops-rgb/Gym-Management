using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Payments.ListMemberPayments;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.ListMyPayments;

/// <summary>The member's own payments and refunds, newest first, through the panel's history (BUSINESS_RULES.md §15).</summary>
public sealed class ListMyPaymentsHandler(IAppDbContext db, ListMemberPaymentsHandler payments)
{
    public async Task<Result<PagedResponse<MyPaymentResponse>>> Handle(
        Guid siteAccountId, ListMemberPaymentsQuery query, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<PagedResponse<MyPaymentResponse>>(member.Error);
        }

        var page = await payments.Handle(member.Value.Id, query, cancellationToken);

        return page.IsFailure
            ? Result.Failure<PagedResponse<MyPaymentResponse>>(page.Error)
            : SitePages.Map(page.Value, MyPaymentResponse.From);
    }
}

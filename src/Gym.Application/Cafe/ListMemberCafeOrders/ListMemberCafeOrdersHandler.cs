using Gym.Application.Cafe.ListCafeOrders;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.ListMemberCafeOrders;

/// <summary>
/// One member's purchase history (task 7.3), under the member the way their payments, visits and
/// subscriptions are. The only thing it adds to the counter's history is that an unknown member
/// is a 404 rather than an empty page, so a mistyped id is not mistaken for "never bought
/// anything".
/// </summary>
public sealed class ListMemberCafeOrdersHandler(IAppDbContext db, ListCafeOrdersHandler orders)
{
    public async Task<Result<PagedResponse<CafeOrderResponse>>> Handle(
        Guid memberId, ListMemberCafeOrdersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberExists = await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            return Result.Failure<PagedResponse<CafeOrderResponse>>(MemberErrors.NotFound);
        }

        return await orders.Handle(
            new ListCafeOrdersQuery(memberId, query.From, query.To, Page: query.Page, PageSize: query.PageSize),
            cancellationToken);
    }
}

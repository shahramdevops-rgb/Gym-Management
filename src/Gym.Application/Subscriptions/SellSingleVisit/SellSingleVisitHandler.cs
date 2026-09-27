using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.SellSingleVisit;

/// <summary>
/// Sells one visit for today at the single-visit price (BUSINESS_RULES.md §4 <i>Single-session
/// subscriptions</i>). Owner and Staff. The desk types nothing: the price is the Owner's setting.
/// </summary>
public sealed class SellSingleVisitHandler(IAppDbContext db, SubscriptionSeller seller)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure<SubscriptionResponse>(MemberErrors.NotFound);
        }

        return await seller.SellSingleVisitAsync(member, cancellationToken);
    }
}

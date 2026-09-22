using Gym.Application.Common;
using Gym.Application.Subscriptions;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.GetMemberDebt;

/// <summary>
/// What the member still owes, item by item (BUSINESS_RULES.md §5 <i>Member debt</i>). Front-desk
/// work, so both roles: the desk has to be able to say what a member owes and take money for it.
/// </summary>
public sealed class GetMemberDebtHandler(IAppDbContext db)
{
    public async Task<Result<MemberDebtResponse>> Handle(Guid memberId, CancellationToken cancellationToken)
    {
        var exists = await db.Members.AsNoTracking().AnyAsync(member => member.Id == memberId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<MemberDebtResponse>(MemberErrors.NotFound);
        }

        var items = await MemberDebt.GetItemsAsync(db, memberId, cancellationToken);

        var planNames = await PlanNames.ByIdAsync(
            db, items.Select(item => item.PlanId).Distinct().ToList(), cancellationToken);

        var breakdown = items
            .Select(item => new MemberDebtItemResponse(
                item.SubscriptionId,
                planNames[item.PlanId],
                item.StartDate,
                item.EndDate,
                item.Price,
                item.NetPaid,
                item.Outstanding))
            .ToList();

        return new MemberDebtResponse(breakdown.Sum(item => item.Outstanding), breakdown);
    }
}

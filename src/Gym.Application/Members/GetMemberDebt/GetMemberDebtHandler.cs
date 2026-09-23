using Gym.Application.Common;
using Gym.Application.Subscriptions;
using Gym.Domain.Common;
using Gym.Domain.Members;
using Gym.Domain.Payments;

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

        // Only the subscription items have a plan to name; a service charge is labelled by its
        // kind, which the frontend translates.
        var planNames = await PlanNames.ByIdAsync(
            db,
            items.Where(item => item.PlanId is not null).Select(item => item.PlanId!.Value).Distinct().ToList(),
            cancellationToken);

        var breakdown = items
            .Select(item => new MemberDebtItemResponse(
                item.Kind,
                item.Id,
                item.Kind == PaymentTargetKind.Subscription ? planNames[item.PlanId!.Value] : null,
                item.ServiceKind,
                item.StartDate,
                item.EndDate,
                item.Price,
                item.NetPaid,
                item.Outstanding))
            .ToList();

        return new MemberDebtResponse(breakdown.Sum(item => item.Outstanding), breakdown);
    }
}

using Gym.Application.Cafe;
using Gym.Application.Common;
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

        // Loaded here rather than in MemberDebt.GetItemsAsync: only this breakdown shows the lines,
        // and settling a debt or totalling the member list has no use for them.
        var orderIds = items
            .Where(item => item.Kind == PaymentTargetKind.CafeOrder)
            .Select(item => item.Id)
            .ToList();

        var cafeLines = (await db.CafeOrderItems.AsNoTracking()
                .Where(line => orderIds.Contains(line.OrderId))
                .OrderBy(line => line.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(line => line.OrderId);

        var breakdown = items
            .Select(item => new MemberDebtItemResponse(
                item.Kind,
                item.Id,
                item.Plan,
                item.ServiceKind,
                item.StartDate,
                item.EndDate,
                item.Price,
                item.NetPaid,
                item.Outstanding,
                item.Kind == PaymentTargetKind.CafeOrder
                    ? cafeLines[item.Id].Select(CafeOrderItemResponse.From).ToList()
                    : []))
            .ToList();

        return new MemberDebtResponse(breakdown.Sum(item => item.Outstanding), breakdown);
    }
}

using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListServiceCharges;

/// <summary>
/// Every هوازی charge in the gym, newest first, with who recorded it and who voided it
/// (BUSINESS_RULES.md §12 <i>History</i>, roadmap 6.5.25). No date limit for either role. Voided
/// charges are listed and marked: the front desk's own lists leave them out, but a history that
/// hid them could not explain a refund.
/// </summary>
public sealed class ListServiceChargesHandler(IAppDbContext db, IUserNames users)
{
    public async Task<PagedResponse<HistoryServiceChargeResponse>> Handle(
        ListServiceChargesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var charges = db.ServiceCharges.AsNoTracking();

        if (query.MemberId is { } memberId)
        {
            charges = charges.Where(charge => charge.MemberId == memberId);
        }

        // By the business date the charge carries: it already is the gym's day (§12).
        if (query.From is { } from)
        {
            charges = charges.Where(charge => charge.ChargedOn >= from);
        }

        if (query.To is { } to)
        {
            charges = charges.Where(charge => charge.ChargedOn <= to);
        }

        var totalCount = await charges.CountAsync(cancellationToken);

        // Newest day first, then newest within the day; Id breaks the last ties.
        var page = await charges
            .OrderByDescending(charge => charge.ChargedOn)
            .ThenByDescending(charge => charge.CreatedAt)
            .ThenByDescending(charge => charge.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Three batched lookups for the whole page, not one per row: whose charge, what has been
        // paid on it, and who recorded or voided it.
        var memberIds = page.Select(charge => charge.MemberId).Distinct().ToList();
        var memberNames = await db.Members.AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);

        var netPaidByCharge = await PaymentLedger.GetNetPaidForServiceChargesAsync(
            db, page.Select(charge => charge.Id).ToList(), cancellationToken);

        var userIds = page.Select(charge => charge.RecordedByUserId)
            .Concat(page.Where(charge => charge.VoidedByUserId is not null).Select(charge => charge.VoidedByUserId!.Value))
            .Distinct()
            .ToList();
        var userNames = await users.FullNamesAsync(userIds, cancellationToken);

        var items = page
            .Select(charge =>
            {
                var netPaid = netPaidByCharge.GetValueOrDefault(charge.Id);

                return new HistoryServiceChargeResponse(
                    charge.Id,
                    charge.MemberId,
                    memberNames.GetValueOrDefault(charge.MemberId) ?? string.Empty,
                    charge.AttendanceId,
                    charge.Kind,
                    charge.Amount,
                    charge.ChargedOn,
                    charge.CreatedAt,
                    userNames.GetValueOrDefault(charge.RecordedByUserId),
                    charge.VoidedAt,
                    charge.VoidReason,
                    charge.VoidedByUserId is { } voidedBy ? userNames.GetValueOrDefault(voidedBy) : null,
                    netPaid,
                    PaymentStatusCalculator.Calculate(charge.Amount, netPaid));
            })
            .ToList();

        return new PagedResponse<HistoryServiceChargeResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

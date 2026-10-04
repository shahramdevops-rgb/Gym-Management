using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Application.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.ListLockers;

/// <summary>The locker list, paged like every list (docs/ARCHITECTURE.md), lowest number first.</summary>
public sealed class ListLockersHandler(IAppDbContext db)
{
    public async Task<PagedResponse<LockerResponse>> Handle(ListLockersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var lockers = db.Lockers.AsNoTracking();
        var openAttendances = db.Attendances.Where(a => a.CheckedOutAt == null);
        var members = db.Members;

        var totalCount = await lockers.CountAsync(cancellationToken);

        var items = await lockers
            .OrderBy(locker => locker.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(LockerResponse.Projection(openAttendances, members))
            .ToListAsync(cancellationToken);

        // Batched for the whole page (MemberDebt), not one query per held locker.
        var holderIds = items
            .Where(locker => locker.OccupiedByMemberId is not null)
            .Select(locker => locker.OccupiedByMemberId!.Value)
            .ToList();
        var debtByMemberId = await MemberDebt.GetTotalsAsync(db, holderIds, cancellationToken);

        // A guest has no account: their door is «بدهکار» while anything bought on their visit is
        // unpaid (BUSINESS_RULES.md §6, §7 Guest visit). Few guests are ever inside at once, so this
        // is a few small queries, not a batch per member.
        var guestDebtByLocker = await GuestDebtByLockerAsync(
            items.Where(locker => locker.OccupiedByGuestName is not null).Select(locker => locker.Id).ToList(),
            cancellationToken);

        items = items
            .Select(locker => locker switch
            {
                { OccupiedByMemberId: { } holderId } => locker with { HolderDebt = debtByMemberId.GetValueOrDefault(holderId) },
                { OccupiedByGuestName: not null } => locker with { HolderDebt = guestDebtByLocker.GetValueOrDefault(locker.Id) },
                _ => locker,
            })
            .ToList();

        return new PagedResponse<LockerResponse>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>What each guest-held locker's visit still owes, cafe and charges, keyed by locker.</summary>
    private async Task<Dictionary<Guid, decimal>> GuestDebtByLockerAsync(
        List<Guid> lockerIds, CancellationToken cancellationToken)
    {
        if (lockerIds.Count == 0)
        {
            return [];
        }

        var visits = await db.Attendances.AsNoTracking()
            .Where(a => a.CheckedOutAt == null && a.LockerId != null && lockerIds.Contains(a.LockerId.Value))
            .Select(a => new { a.Id, LockerId = a.LockerId!.Value })
            .ToListAsync(cancellationToken);

        var visitIds = visits.Select(v => v.Id).ToList();
        var ordersByVisit = await VisitCafeOrders.ByAttendanceAsync(db, visitIds, cancellationToken);
        var chargesByVisit = await VisitServiceCharges.ByAttendanceAsync(db, visitIds, cancellationToken);

        return visits.ToDictionary(
            visit => visit.LockerId,
            visit => ordersByVisit.GetValueOrDefault(visit.Id, []).Sum(order => order.Outstanding)
                + chargesByVisit.GetValueOrDefault(visit.Id, []).Sum(charge => charge.Amount - charge.NetPaid));
    }
}

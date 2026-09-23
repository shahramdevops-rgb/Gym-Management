using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.ListCurrentlyInside;

/// <summary>The front desk's "currently inside" board: everyone with an open attendance (BUSINESS_RULES.md §7).</summary>
public sealed class ListCurrentlyInsideHandler(IAppDbContext db)
{
    public async Task<PagedResponse<CurrentlyInsideResponse>> Handle(ListCurrentlyInsideQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var open = db.Attendances.AsNoTracking().Where(a => a.CheckedOutAt == null);

        var totalCount = await open.CountAsync(cancellationToken);

        var items = await open
            .OrderBy(a => a.CheckedInAt)
            .ThenBy(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CurrentlyInsideResponse.Projection(db.Members, db.Lockers))
            .ToListAsync(cancellationToken);

        var chargesByVisit = await VisitServiceCharges.ByAttendanceAsync(
            db, items.Select(item => item.AttendanceId).ToList(), cancellationToken);

        var rows = items
            .Select(item => item with { ServiceCharges = chargesByVisit.GetValueOrDefault(item.AttendanceId, []) })
            .ToList();

        return new PagedResponse<CurrentlyInsideResponse>(rows, query.Page, query.PageSize, totalCount);
    }
}

using Gym.Application.Common;
using Gym.Application.Common.Paging;

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

        return new PagedResponse<CurrentlyInsideResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

using Gym.Application.Common;
using Gym.Application.Common.Paging;

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

        return new PagedResponse<LockerResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

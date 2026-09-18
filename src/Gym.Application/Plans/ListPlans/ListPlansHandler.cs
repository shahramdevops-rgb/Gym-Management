using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Plans.ListPlans;

/// <summary>The plan list, paged like every list (docs/ARCHITECTURE.md), active plans first.</summary>
public sealed class ListPlansHandler(IAppDbContext db)
{
    public async Task<PagedResponse<PlanResponse>> Handle(ListPlansQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var plans = db.Plans.AsNoTracking();

        if (query.IsActive is { } isActive)
        {
            plans = plans.Where(plan => plan.IsActive == isActive);
        }

        var totalCount = await plans.CountAsync(cancellationToken);

        // Active plans first, because those are the ones being sold; then by name. Id breaks
        // ties so paging never repeats or skips a row.
        var items = await plans
            .OrderByDescending(plan => plan.IsActive)
            .ThenBy(plan => plan.NormalizedName)
            .ThenBy(plan => plan.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(PlanResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlanResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

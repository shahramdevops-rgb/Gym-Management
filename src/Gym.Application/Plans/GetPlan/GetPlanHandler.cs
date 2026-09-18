using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Plans.GetPlan;

/// <summary>One plan, active or not.</summary>
public sealed class GetPlanHandler(IAppDbContext db)
{
    public async Task<Result<PlanResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var plan = await db.Plans
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(PlanResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null ? Result.Failure<PlanResponse>(PlanErrors.NotFound) : plan;
    }
}

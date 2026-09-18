using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Plans.SetPlanActive;

/// <summary>
/// Activates or deactivates a plan. A deactivated plan can no longer be sold; subscriptions
/// already sold are not affected (BUSINESS_RULES.md §3).
/// </summary>
/// <remarks>
/// Repeating either action succeeds and changes nothing. As with members, no <c>Version</c> is
/// asked of the client: the request carries only an intent, and <c>xmin</c> still refuses a
/// save that races another one.
/// </remarks>
public sealed class SetPlanActiveHandler(IAppDbContext db)
{
    public Task<Result<PlanResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        Change(id, plan => plan.Activate(), cancellationToken);

    public Task<Result<PlanResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Change(id, plan => plan.Deactivate(), cancellationToken);

    private async Task<Result<PlanResponse>> Change(Guid id, Action<Plan> change, CancellationToken cancellationToken)
    {
        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<PlanResponse>(PlanErrors.NotFound);
        }

        change(plan);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PlanResponse>(PlanErrors.ChangedConcurrently);
        }

        return PlanResponse.From(plan);
    }
}

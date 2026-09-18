using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Plans.UpdatePlan;

/// <summary>
/// Replaces a plan's details, active or not. Subscriptions already sold keep their snapshot, so
/// nothing else changes (BUSINESS_RULES.md §3).
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdateMemberHandler</c>: the client's <c>Version</c> refuses
/// an edit made on stale data, and <c>xmin</c> refuses a save that races another.
/// </remarks>
public sealed class UpdatePlanHandler(IAppDbContext db)
{
    public async Task<Result<PlanResponse>> Handle(Guid id, UpdatePlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<PlanResponse>(PlanErrors.NotFound);
        }

        if (plan.Version != command.Version)
        {
            return Result.Failure<PlanResponse>(PlanErrors.ChangedConcurrently);
        }

        var updated = plan.Update(command.Name, command.DurationDays, command.SessionCount, command.Price);
        if (updated.IsFailure)
        {
            return Result.Failure<PlanResponse>(updated.Error);
        }

        // Keeping its own name is fine; taking another plan's is not.
        var normalizedName = plan.NormalizedName;
        if (await db.Plans.AnyAsync(p => p.NormalizedName == normalizedName && p.Id != id, cancellationToken))
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == PlanConstraints.UniqueName)
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PlanResponse>(PlanErrors.ChangedConcurrently);
        }

        return PlanResponse.From(plan);
    }
}

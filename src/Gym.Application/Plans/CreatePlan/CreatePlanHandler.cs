using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Plans.CreatePlan;

/// <summary>Adds a plan to what the gym sells (BUSINESS_RULES.md §3). Owner only.</summary>
public sealed class CreatePlanHandler(IAppDbContext db)
{
    public async Task<Result<PlanResponse>> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The entity normalizes the name, so build the plan first and compare its normalized form.
        var created = Plan.Create(command.Name, command.DurationDays, command.SessionCount, command.Price);
        if (created.IsFailure)
        {
            return Result.Failure<PlanResponse>(created.Error);
        }

        var plan = created.Value;
        if (await db.Plans.AnyAsync(p => p.NormalizedName == plan.NormalizedName, cancellationToken))
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }

        // Two requests can pass the check above at once; the unique index decides.
        db.Plans.Add(plan);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == PlanConstraints.UniqueName)
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }

        return PlanResponse.From(plan);
    }
}

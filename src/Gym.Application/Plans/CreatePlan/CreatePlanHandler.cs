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
        var created = Plan.Create(
            command.Name, command.DurationDays, command.SessionCount, command.Price, command.Kind);
        if (created.IsFailure)
        {
            return Result.Failure<PlanResponse>(created.Error);
        }

        var plan = created.Value;
        if (await db.Plans.AnyAsync(p => p.NormalizedName == plan.NormalizedName, cancellationToken))
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }

        // BUSINESS_RULES.md §3: one single-session plan, one walk-in rate. Checked here so the answer
        // names the rule; the partial unique index below decides if two requests arrive at once.
        if (plan.IsSingleSession
            && await db.Plans.AnyAsync(p => p.Kind == PlanKind.SingleSession, cancellationToken))
        {
            return Result.Failure<PlanResponse>(PlanErrors.SingleSessionAlreadyExists);
        }

        // Two requests can pass the checks above at once; the unique indexes decide.
        db.Plans.Add(plan);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == PlanConstraints.UniqueName)
        {
            return Result.Failure<PlanResponse>(PlanErrors.NameAlreadyExists);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == PlanConstraints.UniqueSingleSession)
        {
            return Result.Failure<PlanResponse>(PlanErrors.SingleSessionAlreadyExists);
        }

        return PlanResponse.From(plan);
    }
}

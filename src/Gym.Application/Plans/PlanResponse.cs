using System.Linq.Expressions;

using Gym.Domain.Plans;

namespace Gym.Application.Plans;

/// <param name="SessionCount"><c>null</c> means unlimited sessions.</param>
/// <param name="Version">Sent back with an update, so a stale edit is refused.</param>
public sealed record PlanResponse(
    Guid Id,
    string Name,
    int DurationDays,
    int? SessionCount,
    decimal Price,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL.</summary>
    public static readonly Expression<Func<Plan, PlanResponse>> Projection = plan => new PlanResponse(
        plan.Id,
        plan.Name,
        plan.DurationDays,
        plan.SessionCount,
        plan.Price,
        plan.IsActive,
        plan.Version,
        plan.CreatedAt,
        plan.UpdatedAt);

    public static PlanResponse From(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanResponse(
            plan.Id,
            plan.Name,
            plan.DurationDays,
            plan.SessionCount,
            plan.Price,
            plan.IsActive,
            plan.Version,
            plan.CreatedAt,
            plan.UpdatedAt);
    }
}

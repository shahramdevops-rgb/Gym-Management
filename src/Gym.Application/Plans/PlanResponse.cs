using System.Linq.Expressions;
using System.Text.Json.Serialization;

using Gym.Domain.Plans;

namespace Gym.Application.Plans;

/// <param name="SessionCount"><c>null</c> means unlimited sessions.</param>
/// <param name="Kind">
/// <c>SingleSession</c> is the one walk-in plan (BUSINESS_RULES.md §3). It is set when the plan is
/// created and never changes, so an edit form shows it but does not offer it.
/// </param>
/// <param name="Version">Sent back with an update, so a stale edit is refused.</param>
public sealed record PlanResponse(
    Guid Id,
    string Name,
    int DurationDays,
    int? SessionCount,
    decimal Price,
    bool IsActive,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PlanKind>))] PlanKind Kind,
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
        plan.Kind,
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
            plan.Kind,
            plan.Version,
            plan.CreatedAt,
            plan.UpdatedAt);
    }
}

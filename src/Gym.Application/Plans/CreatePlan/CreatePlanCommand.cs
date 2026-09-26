using System.Text.Json.Serialization;

using Gym.Domain.Plans;

namespace Gym.Application.Plans.CreatePlan;

/// <param name="SessionCount"><c>null</c> for unlimited sessions.</param>
/// <param name="Kind">
/// Omitted means <see cref="PlanKind.Membership"/>. A <see cref="PlanKind.SingleSession"/> plan must
/// be 1 day and 1 session, and only one of them can exist (BUSINESS_RULES.md §3). Sent as text, the
/// same way it comes back, so the wire format does not depend on the order of the enum's members.
/// </param>
public sealed record CreatePlanCommand(
    string Name,
    int DurationDays,
    int? SessionCount,
    decimal Price,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PlanKind>))] PlanKind Kind = PlanKind.Membership);

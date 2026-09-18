namespace Gym.Application.Plans.UpdatePlan;

/// <param name="SessionCount"><c>null</c> for unlimited sessions.</param>
/// <param name="Version">The <c>version</c> from the plan as it was read before editing.</param>
public sealed record UpdatePlanCommand(string Name, int DurationDays, int? SessionCount, decimal Price, uint Version);

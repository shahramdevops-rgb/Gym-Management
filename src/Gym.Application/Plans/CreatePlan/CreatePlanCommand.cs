namespace Gym.Application.Plans.CreatePlan;

/// <param name="SessionCount"><c>null</c> for unlimited sessions.</param>
public sealed record CreatePlanCommand(string Name, int DurationDays, int? SessionCount, decimal Price);

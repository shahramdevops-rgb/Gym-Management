namespace Gym.Application.Lockers.LockerUsage;

/// <summary>
/// Bound from the query string: <c>GET /api/lockers/usage?days=30</c>. The period is the last
/// <see cref="Days"/> of the gym's days, today included (BUSINESS_RULES.md §6 <i>Locker usage map</i>).
/// </summary>
public sealed record LockerUsageQuery(int Days = 30)
{
    /// <summary>The periods the viewer can pick from; nothing else is accepted.</summary>
    public static readonly IReadOnlyList<int> AllowedDays = [7, 30, 90];
}

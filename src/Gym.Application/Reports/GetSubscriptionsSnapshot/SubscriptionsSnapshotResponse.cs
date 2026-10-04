namespace Gym.Application.Reports.GetSubscriptionsSnapshot;

/// <summary>
/// The gym's membership plans as they stand today (BUSINESS_RULES.md §12 <i>Operational reports</i>).
/// Single visits are not plans and are not counted.
/// </summary>
/// <param name="Active">Plans in effect today with a session left.</param>
/// <param name="Frozen">Plans frozen now.</param>
/// <param name="ExpiringSoon">Of <paramref name="Active"/>, those ending within 5 days, today included.</param>
/// <param name="LowSessions">Of <paramref name="Active"/>, those with 3 sessions left or fewer.</param>
public sealed record SubscriptionsSnapshotResponse(
    DateOnly Today,
    int Active,
    int Frozen,
    int ExpiringSoon,
    int LowSessions);

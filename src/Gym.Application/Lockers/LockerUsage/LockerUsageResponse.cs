namespace Gym.Application.Lockers.LockerUsage;

/// <summary>
/// How often each locker was used over a period (BUSINESS_RULES.md §6 <i>Locker usage map</i>).
/// Counts only, never money.
/// </summary>
/// <param name="From">The first day counted, in the gym's time zone.</param>
/// <param name="To">The last day counted: today.</param>
/// <param name="Days">How many days the period covers, <see cref="From"/> to <see cref="To"/> inclusive.</param>
/// <param name="Lockers">Every locker, lowest number first, including those never used.</param>
public sealed record LockerUsageResponse(
    DateOnly From,
    DateOnly To,
    int Days,
    IReadOnlyList<LockerUseCountResponse> Lockers);

/// <param name="Uses">
/// Visits checked in during the period that hold this locker now, cancelled check-ins left out.
/// </param>
public sealed record LockerUseCountResponse(Guid LockerId, int Number, int Uses);

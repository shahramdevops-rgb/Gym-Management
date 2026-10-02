namespace Gym.Application.Attendances.CardioOnlyCheckIn;

/// <param name="LockerId">
/// The locker the desk clicked, or <c>null</c> for a reserve place, under the same conditions as an
/// ordinary check-in (BUSINESS_RULES.md §6). Nothing is sold with a cardio-only visit, so there is no
/// sale here (§7 <i>Cardio-only visit</i>).
/// </param>
public sealed record CardioOnlyCheckInCommand(Guid? LockerId);

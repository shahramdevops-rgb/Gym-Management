namespace Gym.Application.Attendances.GuestCheckIn;

/// <param name="GuestName">The guest's full name as the desk typed it (BUSINESS_RULES.md §7 <i>Guest visit</i>).</param>
/// <param name="LockerId">
/// The locker the desk clicked, or <c>null</c> for a reserve place, under the same conditions as a
/// member's check-in (§6).
/// </param>
public sealed record GuestCheckInCommand(string GuestName, Guid? LockerId);

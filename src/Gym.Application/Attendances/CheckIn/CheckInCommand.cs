namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// The place the desk chose for the visit (BUSINESS_RULES.md §7, step 3): the locker it clicked,
/// or <c>null</c> for a reserve place, which only works when every locker is full (§6).
/// </summary>
public sealed record CheckInCommand(Guid? LockerId);

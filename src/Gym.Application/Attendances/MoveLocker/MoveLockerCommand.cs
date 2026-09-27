namespace Gym.Application.Attendances.MoveLocker;

/// <summary>The locker to move the visit to (BUSINESS_RULES.md §7 <i>Moving to another locker</i>). Always a locker, never a reserve place.</summary>
public sealed record MoveLockerCommand(Guid LockerId);

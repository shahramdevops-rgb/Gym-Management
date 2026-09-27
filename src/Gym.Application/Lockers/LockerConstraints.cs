namespace Gym.Application.Lockers;

/// <summary>
/// Database index and constraint names for lockers, shared with the configuration that creates
/// them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class LockerConstraints
{
    public const string UniqueNumber = "ix_lockers_number";

    /// <summary>The gym's lockers are numbered 1 to <c>Locker.Count</c> (BUSINESS_RULES.md §6).</summary>
    public const string NumberRange = "ck_lockers_number_range";
}

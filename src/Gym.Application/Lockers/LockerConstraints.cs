namespace Gym.Application.Lockers;

/// <summary>
/// Database index and constraint names the locker handlers react to, shared with the
/// configuration that creates them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class LockerConstraints
{
    public const string UniqueNumber = "ix_lockers_number";
}

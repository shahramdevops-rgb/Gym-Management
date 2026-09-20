namespace Gym.Application.Attendances;

/// <summary>
/// Database constraint names the check-in handler reacts to, shared with the migration that
/// creates them.
/// </summary>
public static class AttendanceConstraints
{
    /// <summary>A member has at most one open attendance at a time (BUSINESS_RULES.md §7).</summary>
    public const string OneOpenPerMember = "ix_attendances_one_open_per_member";

    /// <summary>A locker holds at most one open attendance at a time (BUSINESS_RULES.md §7).</summary>
    public const string OneOpenPerLocker = "ix_attendances_one_open_per_locker";
}

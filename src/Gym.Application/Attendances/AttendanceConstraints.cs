namespace Gym.Application.Attendances;

/// <summary>
/// Database constraint names the attendance handlers react to, shared with the configuration that
/// creates them.
/// </summary>
public static class AttendanceConstraints
{
    /// <summary>A member has at most one open attendance at a time (BUSINESS_RULES.md §7).</summary>
    public const string OneOpenPerMember = "ix_attendances_one_open_per_member";

    /// <summary>A locker holds at most one open attendance at a time (BUSINESS_RULES.md §7).</summary>
    public const string OneOpenPerLocker = "ix_attendances_one_open_per_locker";

    /// <summary>A reserve place holds at most one open attendance at a time (BUSINESS_RULES.md §6).</summary>
    public const string OneOpenPerReserveSlot = "ix_attendances_one_open_per_reserve_slot";

    /// <summary>A reserve place is numbered 1 to <c>Attendance.ReservePlaceCount</c> (BUSINESS_RULES.md §6).</summary>
    public const string ReserveSlotRange = "ck_attendances_reserve_slot_range";

    /// <summary>An open visit holds exactly one of a locker and a reserve place (BUSINESS_RULES.md §6).</summary>
    public const string OpenHoldsOnePlace = "ck_attendances_open_holds_one_place";
}

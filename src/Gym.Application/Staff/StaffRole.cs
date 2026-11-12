namespace Gym.Application.Staff;

/// <summary>
/// What an account the Owner makes on «کارمندان» is (BUSINESS_RULES.md §1, §16). Chosen when the
/// account is made and never changed: another role is another account.
/// </summary>
public enum StaffRole
{
    /// <summary>A staff member, «کارمند»: the desk's daily work.</summary>
    Staff,

    /// <summary>A shop admin, «مدیر فروشگاه» (ADR 0011): the online shop only.</summary>
    Shop,
}

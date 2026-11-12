namespace Gym.Infrastructure.Identity;

/// <summary>Role names. See BUSINESS_RULES.md §1 and, for <see cref="Shop"/>, §16 and ADR 0011.</summary>
/// <remarks>
/// <see cref="Owner"/> and <see cref="Staff"/> are seeded by <see cref="IdentitySeeder"/>;
/// <see cref="Staff"/> and <see cref="Shop"/> are also created by <see cref="StaffAccounts"/> the
/// first time the Owner makes such an account.
/// </remarks>
public static class Roles
{
    public const string Owner = "Owner";

    public const string Staff = "Staff";

    /// <summary>
    /// The shop admin, «مدیر فروشگاه» (ADR 0011): signs in on the panel and sees only the online
    /// shop. Every gym policy names its roles, so this one passes none of them.
    /// </summary>
    public const string Shop = "Shop";
}

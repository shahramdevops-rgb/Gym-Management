namespace Gym.Application.SiteAuth;

/// <summary>
/// Database index names the site's sign-in handlers react to, named once here and used by the
/// configuration that creates the index, as <c>MemberConstraints</c> does.
/// </summary>
public static class SiteAuthConstraints
{
    /// <summary>One account per member: what refuses a second account made at the same moment.</summary>
    public const string UniqueMember = "ix_site_accounts_member_id";
}

namespace Gym.Infrastructure.SiteAuth;

/// <summary>
/// The claims in the site's tokens (ADR 0010). Deliberately no <c>sub</c>: the staff's
/// <c>ICurrentUser</c> reads <c>sub</c> as a staff user's id, and a site account is not one, so
/// what a member does on the site is recorded with no user, as «سیستم» (BUSINESS_RULES.md §15).
/// </summary>
public static class SiteClaimNames
{
    /// <summary>The <c>SiteAccount</c>'s id, in an access token.</summary>
    public const string SiteAccount = "site_account";

    /// <summary>
    /// What the account may open, one claim per role: <see cref="SiteRoles.Member"/>,
    /// <see cref="SiteRoles.Trainer"/>, or both (BUSINESS_RULES.md §15).
    /// </summary>
    public const string Role = "site_role";

    /// <summary>The phone a sign-up ticket proves, in E.164.</summary>
    public const string Phone = "phone";

    /// <summary>A unique id per token, so a single token can be identified in logs.</summary>
    public const string TokenId = "jti";
}

/// <summary>The values of <see cref="SiteClaimNames.Role"/>.</summary>
public static class SiteRoles
{
    /// <summary>Only for an active member: a deactivated one's token does not carry it.</summary>
    public const string Member = "member";

    /// <summary>Only while the trainer's period is open: a stopped trainer's token does not carry it.</summary>
    public const string Trainer = "trainer";
}

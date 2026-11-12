namespace Gym.Api.Authorization;

/// <summary>
/// The authorization policy names. Every endpoint names one of these, or says
/// <c>AllowAnonymous()</c>; <c>EndpointAuthorizationTests</c> fails the build otherwise.
/// </summary>
/// <remarks>
/// The permissions table in BUSINESS_RULES.md §1 maps onto these. Each policy except
/// <see cref="PasswordChangeAllowed"/> also includes <see cref="PasswordChangedRequirement"/>,
/// so a user who must change their password is refused everywhere else.
/// </remarks>
public static class Policies
{
    /// <summary>Prices, staff accounts, refunds, reports and the rest of the Owner column.</summary>
    public const string OwnerOnly = "OwnerOnly";

    /// <summary>The daily front-desk work both roles do: members, check-in, payments.</summary>
    public const string StaffOrOwner = "StaffOrOwner";

    /// <summary>
    /// The online shop's work, «مدیر فروشگاه» only (ADR 0011, BUSINESS_RULES.md §16): every change
    /// to the shop. Not the Owner, who reads the shop but does not change it (the developer,
    /// 1405/08/20), and never Staff.
    /// </summary>
    public const string ShopAdmin = "ShopAdmin";

    /// <summary>Reading the online shop: the shop admin, and the Owner who reads it (§16). Never Staff.</summary>
    public const string ShopOrOwner = "ShopOrOwner";

    /// <summary>
    /// Who is signed in on the panel, «me», for the three roles by name. Only that endpoint uses it:
    /// the panel needs it to choose the shop admin's menu (ADR 0011). Never a policy for work.
    /// </summary>
    public const string OwnerStaffOrShop = "OwnerStaffOrShop";

    /// <summary>
    /// Any logged-in user, including one who must change their password. Only the
    /// change-password endpoint uses it.
    /// </summary>
    public const string PasswordChangeAllowed = "PasswordChangeAllowed";

    /// <summary>
    /// Anyone signed in on the site, member or trainer (BUSINESS_RULES.md §15): the site's scheme
    /// only, never a staff token. Not the staff's password gate: site accounts have no password.
    /// </summary>
    public const string SiteAccount = "SiteAccount";

    /// <summary>
    /// A site account that is an active member: the member's area. The site's scheme only; a member
    /// the desk deactivates loses it at the next refresh and sees only the notice in «me».
    /// </summary>
    public const string SiteMember = "SiteMember";

    /// <summary>
    /// A site account that is a trainer now: the trainer's area (15.4 on). The site's scheme only;
    /// a stopped trainer's next token no longer passes it.
    /// </summary>
    public const string SiteTrainer = "SiteTrainer";
}

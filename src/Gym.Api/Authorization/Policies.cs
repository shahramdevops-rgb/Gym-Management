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
    /// <summary>Plans, lockers, staff accounts, refunds, reports and the rest of the Owner column.</summary>
    public const string OwnerOnly = "OwnerOnly";

    /// <summary>The daily front-desk work both roles do: members, check-in, payments.</summary>
    public const string StaffOrOwner = "StaffOrOwner";

    /// <summary>
    /// Any logged-in user, including one who must change their password. Only the
    /// change-password endpoint uses it.
    /// </summary>
    public const string PasswordChangeAllowed = "PasswordChangeAllowed";
}

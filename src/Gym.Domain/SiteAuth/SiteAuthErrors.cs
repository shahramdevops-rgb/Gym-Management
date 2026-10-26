using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>The site's sign-in failures past the code itself (BUSINESS_RULES.md §15).</summary>
public static class SiteAuthErrors
{
    /// <summary>
    /// The refresh cookie is missing, unknown, expired, revoked or a reused copy. One code for all
    /// of them, as for the staff: telling them apart would help only someone testing stolen tokens.
    /// </summary>
    public static readonly Error SessionInvalid = Error.Unauthorized(
        "SiteAuth.SessionInvalid",
        "The session has ended; sign in again.");

    /// <summary>The 15 minutes after the code have passed, or the ticket is not one of ours.</summary>
    public static readonly Error SignUpExpired = Error.BusinessRule(
        "SiteAuth.SignUpExpired",
        "The time to finish signing up has passed; ask for a new code.");

    /// <summary>
    /// Another sign-in made this member's account at the very same moment (the phone was changed at
    /// the desk while both were under way). Rare; signing in again finds the account.
    /// </summary>
    public static readonly Error SignInConflict = Error.Conflict(
        "SiteAuth.SignInConflict",
        "Another sign-in for this member happened at the same moment; ask for a new code and sign in again.");

    public static readonly Error AccountNotFound = Error.NotFound(
        "SiteAuth.AccountNotFound",
        "No site account has that id.");
}

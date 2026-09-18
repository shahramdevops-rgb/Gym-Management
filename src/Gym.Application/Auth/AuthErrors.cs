using Gym.Domain.Common;

namespace Gym.Application.Auth;

/// <summary>
/// Login failures. BUSINESS_RULES.md §1 decides which one a caller sees: an unknown user name
/// and a wrong password are deliberately the same error, so user names cannot be discovered.
/// </summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "The user name or password is incorrect.");

    public static readonly Error LockedOut = Error.Unauthorized(
        "Auth.LockedOut",
        "The account is temporarily locked after too many failed login attempts.");

    public static readonly Error UserInactive = Error.Unauthorized(
        "Auth.UserInactive",
        "The account has been deactivated.");

    /// <summary>
    /// Every refresh failure except an inactive user: missing, unknown, expired, revoked or
    /// reused. One code for all of them, because a caller who could tell them apart would learn
    /// which stolen tokens are still worth trying. The frontend's answer is the same for each:
    /// show the login page.
    /// </summary>
    public static readonly Error RefreshTokenInvalid = Error.Unauthorized(
        "Auth.RefreshTokenInvalid",
        "The refresh token is missing, expired or revoked.");
}

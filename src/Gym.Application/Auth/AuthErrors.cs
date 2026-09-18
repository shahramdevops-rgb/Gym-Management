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
}

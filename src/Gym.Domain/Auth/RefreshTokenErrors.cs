using Gym.Domain.Common;

namespace Gym.Domain.Auth;

/// <summary>
/// Why <see cref="RefreshToken.Rotate"/> refused. The API does not show callers which one
/// happened: every refresh failure reaches them as the same <c>Auth.RefreshTokenInvalid</c>.
/// </summary>
public static class RefreshTokenErrors
{
    public static readonly Error Revoked = Error.Unauthorized(
        "RefreshTokens.Revoked",
        "The refresh token has been revoked.");

    public static readonly Error Expired = Error.Unauthorized(
        "RefreshTokens.Expired",
        "The refresh token has expired.");
}

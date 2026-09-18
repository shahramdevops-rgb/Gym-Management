using Gym.Application.Common.Security;

namespace Gym.Application.Auth;

/// <summary>
/// What login and refresh hand back to the endpoint: the response body, plus the new refresh
/// token, which the endpoint puts in the cookie and never in the body.
/// </summary>
public sealed record AuthSession(AccessTokenResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)
{
    public static AuthSession Create(
        AuthenticatedUser user,
        AccessToken accessToken,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(accessToken);

        return new AuthSession(
            new AccessTokenResponse(accessToken.Value, accessToken.ExpiresAt, user.MustChangePassword),
            refreshToken,
            refreshTokenExpiresAt);
    }
}

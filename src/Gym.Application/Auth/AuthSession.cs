using Gym.Application.Common.Security;

namespace Gym.Application.Auth;

/// <summary>
/// What login and refresh hand back to the endpoint: the response body, plus the new refresh
/// token, which the endpoint puts in the cookie and never in the body.
/// </summary>
public sealed record AuthSession(AccessTokenResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)
{
    /// <summary>
    /// The trusted-device secret for the <c>gym_device</c> cookie. Set by login, and by a refresh
    /// from a trusted device; null means "leave the cookie as it is".
    /// </summary>
    public string? DeviceToken { get; init; }

    public DateTimeOffset? DeviceTokenExpiresAt { get; init; }

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

namespace Gym.Application.Auth;

/// <summary>
/// The body returned by login and refresh: the access token and when it expires, so the
/// frontend knows when to refresh. <see cref="MustChangePassword"/> is repeated outside the
/// token so the frontend can open the change-password screen without decoding the JWT.
/// </summary>
/// <remarks>
/// The refresh token is deliberately not here. It travels only in an HttpOnly cookie, which
/// JavaScript cannot read, so a script injected into the page cannot steal it.
/// </remarks>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, bool MustChangePassword);

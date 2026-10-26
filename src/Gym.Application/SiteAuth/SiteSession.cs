using Gym.Application.SiteAuth.GetMe;

namespace Gym.Application.SiteAuth;

/// <summary>
/// What a sign-in, a sign-up and a refresh hand back to the endpoint: the response body, plus the
/// new refresh token, which the endpoint puts in the site's cookie and never in the body.
/// </summary>
public sealed record SiteSession(SiteAccessResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>The body: the access token, when it expires, and who signed in.</summary>
public sealed record SiteAccessResponse(string AccessToken, DateTimeOffset ExpiresAt, SiteMeResponse Me);

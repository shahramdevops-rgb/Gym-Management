namespace Gym.Api.Common;

/// <summary>
/// The one place that writes, reads and clears the refresh token cookie, so its attributes
/// cannot drift between login, refresh and logout.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>HttpOnly</b>: JavaScript cannot read it, so a script injected into the page cannot steal it.</item>
/// <item><b>Secure</b>: only sent over HTTPS. Browsers treat <c>http://localhost</c> as secure, so
/// local development still works.</item>
/// <item><b>SameSite=Strict</b>: never sent on a request that starts on another site, which is
/// what stops a malicious page from making the browser call refresh or logout.</item>
/// <item><b>Path=/api/auth</b>: sent only to the auth endpoints, not with every API call.</item>
/// </list>
/// Clearing a cookie only works with the same name and path it was set with, which is the
/// other reason these live together.
/// </remarks>
public static class RefreshTokenCookie
{
    public const string Name = "gym_refresh";

    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies[Name];
    }

    public static void Write(HttpResponse response, string value, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, value, CreateOptions(expiresAt));
    }

    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(Name, CreateOptions(expiresAt: null));
    }

    private static CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,

        // Needed for login to work at all, so it is not subject to any cookie-consent policy.
        IsEssential = true,
    };
}

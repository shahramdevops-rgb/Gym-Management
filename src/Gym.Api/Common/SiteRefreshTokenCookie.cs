namespace Gym.Api.Common;

/// <summary>
/// The site's refresh cookie (ADR 0010): the same attributes as the staff's
/// (<see cref="RefreshTokenCookie"/>: HttpOnly, Secure, SameSite=Strict), its own name, and a path
/// only the site's sign-in endpoints are under. On <c>app.pasargadgymplus.ir</c> it never reaches
/// the panel's host, and the panel's never reaches the site.
/// </summary>
public static class SiteRefreshTokenCookie
{
    public const string Name = "gym_site_refresh";

    public const string Path = "/api/site/auth";

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
        IsEssential = true,
    };
}

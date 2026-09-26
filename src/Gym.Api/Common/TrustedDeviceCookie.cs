namespace Gym.Api.Common;

/// <summary>
/// The <c>gym_device</c> cookie that marks a browser as trusted after a successful login
/// (BUSINESS_RULES.md §1 *Lockout*, ADR 0004). The same attributes as the refresh cookie, for the
/// same reasons (see <see cref="RefreshTokenCookie"/>): JavaScript cannot read it, it is never sent
/// from another site, and only the auth endpoints receive it.
/// </summary>
/// <remarks>
/// Unlike the refresh cookie, a failed login never clears it: a wrong password typed on the
/// front-desk PC must not turn that PC into an unknown device, which is the whole point.
/// </remarks>
public static class TrustedDeviceCookie
{
    public const string Name = "gym_device";

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies[Name];
    }

    public static void Write(HttpResponse response, string value, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshTokenCookie.Path,
            Expires = expiresAt,
            IsEssential = true,
        });
    }
}

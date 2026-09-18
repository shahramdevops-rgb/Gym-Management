using Gym.Api.Common;

using Microsoft.Net.Http.Headers;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>Reads the refresh cookie from responses and sends it on requests, by hand.</summary>
internal static class RefreshCookies
{
    public const string RefreshPath = "/api/auth/refresh";
    public const string LogoutPath = "/api/auth/logout";

    /// <summary>The parsed <c>Set-Cookie</c> for the refresh token, or null if none was sent.</summary>
    public static SetCookieHeaderValue? GetRefreshCookie(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return null;
        }

        return SetCookieHeaderValue.ParseList([.. values])
            .SingleOrDefault(cookie => cookie.Name.Value == RefreshTokenCookie.Name);
    }

    /// <summary>The refresh token value a successful login or refresh put in the cookie.</summary>
    public static string ReadRefreshToken(this HttpResponseMessage response) =>
        response.GetRefreshCookie().ShouldNotBeNull("expected a refresh cookie").Value.Value.ShouldNotBeNull();

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(RefreshPath, refreshToken), TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(LogoutPath, refreshToken), TestContext.Current.CancellationToken);

    private static HttpRequestMessage WithCookie(string path, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{RefreshTokenCookie.Name}={refreshToken}");
        }

        return request;
    }
}

using System.Net.Http.Json;

using Gym.Api.Common;

using Microsoft.Net.Http.Headers;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// Reads the trusted-device cookie from responses and sends it on requests, by hand, for the same
/// reason as <see cref="RefreshCookies"/>: the test server speaks plain HTTP and the cookie is Secure.
/// </summary>
internal static class DeviceCookies
{
    public static SetCookieHeaderValue? GetDeviceCookie(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return null;
        }

        return SetCookieHeaderValue.ParseList([.. values])
            .SingleOrDefault(cookie => cookie.Name.Value == TrustedDeviceCookie.Name);
    }

    /// <summary>The device secret a successful login put in the cookie.</summary>
    public static string ReadDeviceToken(this HttpResponseMessage response) =>
        response.GetDeviceCookie().ShouldNotBeNull("expected a device cookie").Value.Value.ShouldNotBeNull();

    /// <summary>A login from a browser that holds <paramref name="deviceToken"/> in its cookie, or none.</summary>
    public static Task<HttpResponseMessage> LoginFromDeviceAsync(
        this HttpClient client,
        string userName,
        string password,
        string? deviceToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginClient.LoginPath)
        {
            Content = JsonContent.Create(new { userName, password }),
        };

        return client.SendAsync(WithDevice(request, deviceToken), TestContext.Current.CancellationToken);
    }

    /// <summary>A refresh from a browser holding both cookies.</summary>
    public static Task<HttpResponseMessage> RefreshFromDeviceAsync(this HttpClient client, string refreshToken, string? deviceToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshCookies.RefreshPath);
        var cookies = deviceToken is null
            ? $"{RefreshTokenCookie.Name}={refreshToken}"
            : $"{RefreshTokenCookie.Name}={refreshToken}; {TrustedDeviceCookie.Name}={deviceToken}";
        request.Headers.Add(HeaderNames.Cookie, cookies);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> ChangePasswordFromDeviceAsync(
        this HttpClient client,
        string accessToken,
        string currentPassword,
        string newPassword,
        string? deviceToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };

        return client.SendAsync(WithDevice(request, deviceToken).WithBearer(accessToken), TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage WithDevice(HttpRequestMessage request, string? deviceToken)
    {
        if (deviceToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{TrustedDeviceCookie.Name}={deviceToken}");
        }

        return request;
    }
}

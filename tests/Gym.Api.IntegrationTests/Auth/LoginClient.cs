using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Application.Auth;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>Posts to the login endpoint and reads the <c>code</c> field of a ProblemDetails reply.</summary>
internal static class LoginClient
{
    public const string LoginPath = "/api/auth/login";

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync(LoginPath, new { userName, password }, TestContext.Current.CancellationToken);

    /// <summary>Logs in and returns the access token, failing the test if login does not succeed.</summary>
    public static async Task<string> LoginForAccessTokenAsync(this HttpClient client, string userName, string password)
    {
        using var response = await client.LoginAsync(userName, password);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);

        return body.ShouldNotBeNull().AccessToken;
    }

    /// <summary>A request carrying the access token in the Authorization header.</summary>
    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

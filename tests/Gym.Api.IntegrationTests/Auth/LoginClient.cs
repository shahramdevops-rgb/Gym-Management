using System.Net.Http.Json;
using System.Text.Json;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>Posts to the login endpoint and reads the <c>code</c> field of a ProblemDetails reply.</summary>
internal static class LoginClient
{
    public const string LoginPath = "/api/auth/login";

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync(LoginPath, new { userName, password }, TestContext.Current.CancellationToken);

    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.Common;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Security;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// The site's sign-in as the app will drive it: ask for a code, read it the way only a test can
/// (from the row's seed and the server's secret), and type it.
/// </summary>
internal static class SiteClient
{
    public const string CodePath = "/api/site/auth/code";
    public const string SignInPath = "/api/site/auth/sign-in";
    public const string SignUpPath = "/api/site/auth/sign-up";
    public const string RefreshPath = "/api/site/auth/refresh";
    public const string LogoutPath = "/api/site/auth/logout";
    public const string MePath = "/api/site/me";

    /// <summary>Asks for a code for <paramref name="phone"/> and returns it.</summary>
    public static async Task<string> RequestCodeAsync(this HttpClient client, DatabaseFixture fixture, string phone)
    {
        using var response = await client.PostAsJsonAsync(CodePath, new { phoneNumber = phone }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        await using var scope = fixture.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SignInCodes
            .AsNoTracking()
            .SingleAsync(code => code.PhoneNumber == phone, TestContext.Current.CancellationToken);

        return scope.ServiceProvider.GetRequiredService<ISignInCodes>().CodeFor(row.PhoneNumber, row.CodeSeed);
    }

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string phone, string code) =>
        client.PostAsJsonAsync(SignInPath, new { phoneNumber = phone, code }, TestContext.Current.CancellationToken);

    /// <summary>A code asked for and typed: the sign-in's response.</summary>
    public static async Task<HttpResponseMessage> SignInWithCodeAsync(this HttpClient client, DatabaseFixture fixture, string phone) =>
        await client.SignInAsync(phone, await client.RequestCodeAsync(fixture, phone));

    public static Task<HttpResponseMessage> SignUpAsync(this HttpClient client, string ticket, string fullName, DateOnly? birthDate) =>
        client.PostAsJsonAsync(SignUpPath, new { ticket, fullName, birthDate }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(RefreshPath, refreshToken), TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(LogoutPath, refreshToken), TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetMeAsync(this HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MePath);
        request.Headers.Authorization = new("Bearer", accessToken);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        return body.RootElement.Clone();
    }

    /// <summary>The site's refresh cookie a response set; <c>null</c> when it set none.</summary>
    public static SetCookieHeaderValue? GetSiteRefreshCookie(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return null;
        }

        return SetCookieHeaderValue.ParseList([.. values])
            .SingleOrDefault(cookie => cookie.Name.Value == SiteRefreshTokenCookie.Name);
    }

    public static string ReadSiteRefreshToken(this HttpResponseMessage response) =>
        response.GetSiteRefreshCookie().ShouldNotBeNull("expected the site's refresh cookie").Value.Value.ShouldNotBeNull();

    private static HttpRequestMessage WithCookie(string path, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{SiteRefreshTokenCookie.Name}={refreshToken}");
        }

        return request;
    }
}

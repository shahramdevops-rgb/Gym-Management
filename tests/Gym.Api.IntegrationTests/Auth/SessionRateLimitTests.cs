using System.Net;

using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// The per-IP limit on refresh and logout (BUSINESS_RULES.md §1: 60 a minute, counted together).
/// Runs against its own host with a limit of 3, because the shared test host raises the limit far
/// above the real one (see GymApiFactory).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SessionRateLimitTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int Limit = 3;

    [Fact]
    public async Task Refresh_MoreRequestsThanTheLimitFromOneAddress_Returns429ProblemDetails()
    {
        using var client = CreateLimitedClient();

        for (var attempt = 1; attempt <= Limit; attempt++)
        {
            using var allowed = await client.RefreshAsync(refreshToken: null);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"refresh {attempt} is within the limit");
        }

        using var refused = await client.RefreshAsync(refreshToken: null);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await refused.ReadErrorCodeAsync()).ShouldBe("General.TooManyRequests");
    }

    [Fact]
    public async Task Logout_AfterRefreshesUsedTheLimit_Returns429()
    {
        using var client = CreateLimitedClient();

        for (var attempt = 1; attempt <= Limit; attempt++)
        {
            using var allowed = await client.RefreshAsync(refreshToken: null);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var refused = await client.LogoutAsync(refreshToken: null);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await refused.ReadErrorCodeAsync()).ShouldBe("General.TooManyRequests");
    }

    [Fact]
    public async Task Login_AfterRefreshesUsedTheLimit_IsStillAllowed()
    {
        using var client = CreateLimitedClient();

        for (var attempt = 1; attempt <= Limit; attempt++)
        {
            using var allowed = await client.RefreshAsync(refreshToken: null);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Login has its own bucket: a desk whose tabs refreshed a lot can still sign someone in.
        using var login = await client.LoginAsync("nobody", TestUsers.Password);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateLimitedClient() => Fixture.CreateClient(new Dictionary<string, string?>
    {
        [$"{SessionRateLimitOptions.SectionName}:PermitLimit"] = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
    });
}

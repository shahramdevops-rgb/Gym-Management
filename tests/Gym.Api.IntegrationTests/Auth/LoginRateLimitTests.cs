using System.Net;

using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// The per-IP login limit. Runs against its own host with a limit of 3, because the shared test
/// host raises the limit far above the real one (see GymApiFactory).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LoginRateLimitTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Login_MoreAttemptsThanTheLimitFromOneAddress_Returns429ProblemDetails()
    {
        using var client = Fixture.CreateClient(new Dictionary<string, string?>
        {
            [$"{LoginRateLimitOptions.SectionName}:PermitLimit"] = "3",
        });

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var allowed = await client.LoginAsync("nobody", TestUsers.Password);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"attempt {attempt} is within the limit");
        }

        using var refused = await client.LoginAsync("nobody", TestUsers.Password);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await refused.ReadErrorCodeAsync()).ShouldBe("General.TooManyRequests");
    }
}

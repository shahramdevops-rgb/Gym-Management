using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// The sign-in code shown on the developer's computer (<c>SiteDevelopmentCodeEndpoints</c>) exists
/// only in the Development environment: anywhere else, the server included, the path is not there.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DevelopmentCodeTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task DevelopmentCode_OutsideDevelopment_IsNotThere()
    {
        using var client = Fixture.CreateClient();
        using var asked = await client.PostAsJsonAsync(
            "/api/site/auth/code", new { phoneNumber = "09121234567" }, TestContext.Current.CancellationToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await client.GetAsync(
            "/api/site/auth/development-code?phoneNumber=09121234567", TestContext.Current.CancellationToken);

        // No endpoint at all: the fallback policy answers an unknown path with 401, never the code.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

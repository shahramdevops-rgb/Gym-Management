using System.Net;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Health;

/// <summary>
/// Task 0.6's "done when", and the first test that exercises the whole application: the real
/// <c>Program.cs</c>, the real DI container and a real Postgres.
/// </summary>
/// <remarks>
/// One assertion, a lot of coverage. For this to return <c>Healthy</c>, the host has to start,
/// <c>AddInfrastructure</c> has to build a working connection string, the migrations have to
/// apply, <c>/health</c> has to be mapped, and <c>AddDbContextCheck</c> has to reach the
/// database. No unit test can cover that list, because every item on it is a seam between two
/// things rather than a thing.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class HealthEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task GetHealth_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldBe("Healthy");
    }

    [Fact]
    public async Task GetHealth_WhenCalled_ReturnsTheCorrelationIdHeader()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Proves the middleware is in the pipeline, not merely that the class works — the
        // unit test in CorrelationIdMiddlewareTests cannot tell whether anyone wired it up.
        response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
    }
}

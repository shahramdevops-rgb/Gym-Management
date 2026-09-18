using Gym.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// CLAUDE.md: "Every endpoint has an explicit authorization policy." This turns that sentence
/// from something a reviewer has to notice into something the test suite checks on every run.
/// </summary>
/// <remarks>
/// The fallback policy already makes a forgotten policy fail closed. This test catches the
/// other half: an endpoint that is closed by accident, when the author meant to decide.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class EndpointAuthorizationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Endpoints_WhenMapped_EachDeclareAPolicyOrAllowAnonymous()
    {
        await using var scope = Fixture.CreateScope();
        var endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.ShouldNotBeEmpty("the check below proves nothing if no endpoints were found.");

        var undeclared = endpoints
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName)
            .ToList();

        undeclared.ShouldBeEmpty("add RequireAuthorization(Policies.X) or AllowAnonymous() to these endpoints.");
    }
}

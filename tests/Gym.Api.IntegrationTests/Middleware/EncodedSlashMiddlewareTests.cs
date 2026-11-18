using System.Net;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Gym.Api.IntegrationTests.Middleware;

/// <summary>
/// A path with an encoded slash or backslash is refused before anything routes it (16.4's review):
/// Caddy and Kestrel would otherwise disagree on where it ends, and the gym's public address could
/// reach more of the API than its one shop path.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class EncodedSlashMiddlewareTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Theory]
    [InlineData("/api/site/shop/a%2fb%2fc/../../../auth/login")]
    [InlineData("/api/site/shop/a%2Fb/%2e%2e/%2e%2e/auth/code")]
    [InlineData("/api/site/shop/products/a%5Cb")]
    public void HasEncodedSlash_ThePathAsSent_IsCaught(string rawTarget) =>
        EncodedSlashMiddleware.HasEncodedSlash(ContextWithRawTarget(rawTarget)).ShouldBeTrue();

    [Theory]
    [InlineData("/api/site/shop/showcase")]
    [InlineData("/api/members?search=a%2Fb")]
    [InlineData("/api/site/shop/products/0199b000-0000-7000-8000-000000000011")]
    public void HasEncodedSlash_AnOrdinaryPathOrAQuery_IsLetThrough(string rawTarget) =>
        EncodedSlashMiddleware.HasEncodedSlash(ContextWithRawTarget(rawTarget)).ShouldBeFalse();

    [Theory]
    [InlineData("/api/site/shop/products/a%2Fb")]
    [InlineData("/api/auth/login%2F")]
    public async Task Request_WithAnEncodedSlash_Returns400(string path)
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("General.InvalidPath");
    }

    [Fact]
    public async Task Request_AnEncodedSlashInTheQuery_IsAnsweredAsUsual()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync("/api/site/shop/showcase?from=a%2Fb", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static DefaultHttpContext ContextWithRawTarget(string rawTarget)
    {
        var context = new DefaultHttpContext();
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = rawTarget;
        return context;
    }
}

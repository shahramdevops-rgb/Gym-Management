using System.Net;

using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Configuration;

/// <summary>
/// Behind Caddy every request comes from Caddy's address. These tests stand in for Caddy: the
/// test server reports a chosen peer address, and the client sends <c>X-Forwarded-For</c> the way
/// Caddy would. The observable effect is the per-IP login limit (limit 2 here).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ForwardedHeadersTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrustedNetwork = "10.0.0.0/8";
    private const string ProxyInsideTheNetwork = "10.1.2.3";
    private const string StrangerOutsideTheNetwork = "192.0.2.7";

    [Fact]
    public async Task Login_ProxyInTrustedNetworkForwardsTwoClients_EachClientHasItsOwnLimit()
    {
        using var client = CreateClient(TrustedNetwork, ProxyInsideTheNetwork);

        (await LoginFromAsync(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.1")).ShouldBe(HttpStatusCode.TooManyRequests);

        (await LoginFromAsync(client, "203.0.113.2")).ShouldBe(
            HttpStatusCode.Unauthorized, "a different client behind the same proxy has a limit of its own");
    }

    [Fact]
    public async Task Login_SenderOutsideTheTrustedNetworkForgesForwardedFor_StaysInOneBucket()
    {
        using var client = CreateClient(TrustedNetwork, StrangerOutsideTheNetwork);

        (await LoginFromAsync(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.3")).ShouldBe(
            HttpStatusCode.TooManyRequests, "an invented address per request must not escape the limit");
    }

    [Fact]
    public async Task Login_TrustedNetworkNotConfigured_IgnoresForwardedFor()
    {
        using var client = CreateClient(trustedNetwork: null, ProxyInsideTheNetwork);

        (await LoginFromAsync(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginFromAsync(client, "203.0.113.3")).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void Startup_TrustedNetworkIsNotCidr_FailsWithAnActionableMessage()
    {
        var exception = Should.Throw<InvalidOperationException>(() => CreateClient("not-a-network", ProxyInsideTheNetwork));

        exception.Message.ShouldContain(ForwardedHeadersConfiguration.TrustedNetworkKey);
    }

    private HttpClient CreateClient(string? trustedNetwork, string peerAddress)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{LoginRateLimitOptions.SectionName}:PermitLimit"] = "2",
            [ForwardedHeadersConfiguration.TrustedNetworkKey] = trustedNetwork,
        };

        return Fixture.CreateClient(
            settings,
            builder => builder.ConfigureServices(services =>
                services.AddTransient<IStartupFilter>(_ => new PeerAddressFilter(IPAddress.Parse(peerAddress)))));
    }

    private static async Task<HttpStatusCode> LoginFromAsync(HttpClient client, string forwardedFor)
    {
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

        using var response = await client.LoginAsync("nobody", TestUsers.Password);
        return response.StatusCode;
    }

    /// <summary>
    /// The test server has no network peer, so this gives every request one, ahead of the
    /// application's own middleware. It plays the part of the connection from Caddy.
    /// </summary>
    private sealed class PeerAddressFilter(IPAddress peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use((context, pipeline) =>
                {
                    context.Connection.RemoteIpAddress = peer;
                    return pipeline(context);
                });

                next(app);
            };
    }
}

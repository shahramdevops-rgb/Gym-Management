using System.Net;
using System.Text.RegularExpressions;

using Gym.Api.Authorization;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// ADR 0010: "A site token must never pass a staff endpoint, nor a staff token a site one." Every
/// endpoint the app maps is called with the other kind of token, so an endpoint added later is
/// covered without anyone remembering to add it here.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed partial class SchemeSeparationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";

    [Fact]
    public async Task StaffEndpoints_WithASiteToken_AreAllRefused()
    {
        using var client = Fixture.CreateClient();
        var siteToken = await SiteTokenAsync(client);

        var staffEndpoints = ProtectedEndpoints().Where(endpoint => !endpoint.IsSite).ToList();
        staffEndpoints.Count.ShouldBeGreaterThan(50, "the check below proves nothing if the staff's endpoints were not found.");

        var passed = await PassingAsync(client, staffEndpoints, siteToken);

        passed.ShouldBeEmpty("a site token must never pass a staff endpoint (ADR 0010).");
    }

    [Fact]
    public async Task SiteEndpoints_WithAStaffToken_AreAllRefused()
    {
        await TestUsers.CreateAsync(Fixture, "owner", Roles.Owner);
        using var client = Fixture.CreateClient();
        var staffToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);

        var siteEndpoints = ProtectedEndpoints().Where(endpoint => endpoint.IsSite).ToList();
        siteEndpoints.ShouldNotBeEmpty("the check below proves nothing if the site's endpoints were not found.");

        var passed = await PassingAsync(client, siteEndpoints, staffToken);

        passed.ShouldBeEmpty("a staff token must never pass a site endpoint (ADR 0010).");
    }

    [Fact]
    public async Task SiteEndpoint_WithItsOwnToken_Passes()
    {
        // The other half: the refusals above are about the token, not an endpoint that refuses everyone.
        using var client = Fixture.CreateClient();
        var siteToken = await SiteTokenAsync(client);

        using var response = await client.GetMeAsync(siteToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The endpoints that answered anything but 401 to the token.</summary>
    private static async Task<List<string>> PassingAsync(HttpClient client, IEnumerable<ProtectedEndpoint> endpoints, string accessToken)
    {
        var passed = new List<string>();
        foreach (var endpoint in endpoints)
        {
            using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Path).WithBearer(accessToken);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                passed.Add($"{endpoint.Method} {endpoint.Path}: {(int)response.StatusCode}");
            }
        }

        return passed;
    }

    /// <summary>Every endpoint that is not anonymous, with its route values filled in.</summary>
    private List<ProtectedEndpoint> ProtectedEndpoints() =>
        [.. Fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is not null)
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => new ProtectedEndpoint(
                method,
                Fill(endpoint.RoutePattern.RawText!),
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => IsSitePolicy(data.Policy)))))];

    /// <summary>Every policy that names the site's scheme: the member's and trainer's areas too (15.2).</summary>
    private static bool IsSitePolicy(string? policy) =>
        policy is Policies.SiteAccount or Policies.SiteMember or Policies.SiteTrainer;

    /// <summary>An int where the route wants one, a new id everywhere else.</summary>
    private static string Fill(string route) =>
        RouteValue().Replace(route, match => match.Groups["type"].Value == "int" ? "1" : Guid.CreateVersion7().ToString());

    private async Task<string> SiteTokenAsync(HttpClient client)
    {
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(TestMembers.Seed("سارا محمدی", Phone));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(:(?<type>[a-z]+))?[^}]*\}")]
    private static partial Regex RouteValue();

    private sealed record ProtectedEndpoint(string Method, string Path, bool IsSite);
}

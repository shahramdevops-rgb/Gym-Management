using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.SiteAuth.RequestCode;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// <c>POST /api/site/auth/code</c>: the wiring, through the real pipeline with the fake sender. The
/// rules themselves are in <see cref="RequestCodeTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SiteCodeEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/site/auth/code";

    [Fact]
    public async Task Post_WithNoToken_SendsTheCode()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(Path, new { phoneNumber = "09121234567" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(TestContext.Current.CancellationToken);
        body!["outcome"].ToString().ShouldBe(nameof(RequestCodeOutcome.Sent));
    }

    [Fact]
    public async Task Post_EmptyPhone_Returns400WithTheMembersCodeOnTheField()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(Path, new { phoneNumber = "" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        body.RootElement.GetProperty("errors").GetProperty("phoneNumber")[0].GetProperty("code").GetString()
            .ShouldBe("Members.PhoneRequired");
    }

    [Fact]
    public async Task Post_AfterTheDaysLimit_Returns422WithItsCode()
    {
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // The host runs on the real clock, so the sends are "now" by that clock.
            var now = TimeProvider.System.GetUtcNow();
            for (var i = 0; i < SignInCode.MaxSendsPerGymDay; i++)
            {
                db.SignInCodeSends.Add(SignInCodeSend.Record($"+98935{i:D7}", now, isSendAgain: false, i + 1, 1_150m));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(Path, new { phoneNumber = "09121234567" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(SignInCodeErrors.GymDailyLimitReached.Code);
    }

    [Fact]
    public async Task Post_MoreRequestsThanTheLimitFromOneAddress_Returns429()
    {
        using var client = Fixture.CreateClient(new Dictionary<string, string?>
        {
            [$"{SiteCodeRateLimitOptions.SectionName}:PermitLimit"] = "2",
        });

        for (var request = 1; request <= 2; request++)
        {
            using var allowed = await client.PostAsJsonAsync(Path, new { phoneNumber = $"0912123456{request}" }, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, $"request {request} is within the limit");
        }

        using var refused = await client.PostAsJsonAsync(Path, new { phoneNumber = "09121234569" }, TestContext.Current.CancellationToken);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await refused.ReadErrorCodeAsync()).ShouldBe("General.TooManyRequests");
    }

    [Fact]
    public async Task Post_TheCodesRow_NeverWritesTheSeedToTheAuditLog()
    {
        using var client = Fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(Path, new { phoneNumber = "09121234567" }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = (await db.SignInCodes.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).CodeSeed;
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(SignInCode))
            .ToListAsync(TestContext.Current.CancellationToken);

        audit.ShouldNotBeEmpty();
        audit.ShouldAllBe(log => !(log.NewValues ?? string.Empty).Contains(seed) && !(log.NewValues ?? string.Empty).Contains(nameof(SignInCode.CodeSeed)));
    }
}

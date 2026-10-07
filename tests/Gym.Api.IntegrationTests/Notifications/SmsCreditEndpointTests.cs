using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.GetSmsCredit;
using Gym.Infrastructure.Identity;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// <c>GET /api/sms/credit</c>: the remaining credit on the settings page (BUSINESS_RULES.md §10
/// <i>Sending</i>, task 10.4). Owner only. The test host's provider is the fake one, so it is in test mode.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsCreditEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/credit";

    [Fact]
    public async Task Get_FakeProvider_IsTestModeWithNoCredit()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var credit = await response.Content.ReadFromJsonAsync<SmsCreditResponse>(TestContext.Current.CancellationToken);
        credit.ShouldBe(new SmsCreditResponse(IsTestMode: true, RemainingToman: null));
    }

    [Fact]
    public async Task Get_AsStaff_Returns403()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void From_RemainingRial_IsShownInToman()
    {
        SmsCreditResponse.From(new SmsCredit(IsTestMode: false, RemainingRial: 1_250_000m))
            .ShouldBe(new SmsCreditResponse(IsTestMode: false, RemainingToman: 125_000m));
    }

    [Fact]
    public void From_ProviderNotAnswering_HasNoCredit()
    {
        SmsCreditResponse.From(SmsCredit.Unavailable).ShouldBe(new SmsCreditResponse(IsTestMode: false, RemainingToman: null));
    }
}

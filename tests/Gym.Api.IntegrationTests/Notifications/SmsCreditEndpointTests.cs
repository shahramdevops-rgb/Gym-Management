using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications.GetSmsCredit;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// <c>GET /api/sms/credit</c>: the remaining credit on the settings page (BUSINESS_RULES.md §10
/// <i>Sending</i>, task 10.4) and the credit warning (task 10.5). Owner only. The test host's
/// provider is the fake one, so it is in test mode.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsCreditEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/credit";

    private static int _phoneSuffix;

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
        credit.ShouldBe(new SmsCreditResponse(IsTestMode: true, RemainingToman: null, CreditUsedUp: false));
    }

    // ---- The credit warning (task 10.5) ----

    [Fact]
    public async Task Get_CreditFailureNewerThanTheLastSent_Warns()
    {
        await AddAsync(sentAt: Moment(9));
        await AddAsync(failedAt: Moment(10), code: SmsProviderCodes.CreditUsedUp);

        (await GetAsync()).CreditUsedUp.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_CreditFailureAndNothingEverSent_Warns()
    {
        await AddAsync(failedAt: Moment(10), code: SmsProviderCodes.CreditUsedUp);

        (await GetAsync()).CreditUsedUp.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_SentAfterTheCreditFailure_DoesNotWarn()
    {
        await AddAsync(failedAt: Moment(10), code: SmsProviderCodes.CreditUsedUp);
        await AddAsync(sentAt: Moment(11));

        (await GetAsync()).CreditUsedUp.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_OtherFailuresOnly_DoesNotWarn()
    {
        await AddAsync(failedAt: Moment(10), code: 424);

        (await GetAsync()).CreditUsedUp.ShouldBeFalse();
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
        SmsCreditResponse.From(new SmsCredit(IsTestMode: false, RemainingRial: 1_250_000m), creditUsedUp: false)
            .ShouldBe(new SmsCreditResponse(IsTestMode: false, RemainingToman: 125_000m, CreditUsedUp: false));
    }

    [Fact]
    public void From_ProviderNotAnswering_HasNoCredit()
    {
        SmsCreditResponse.From(SmsCredit.Unavailable, creditUsedUp: false)
            .ShouldBe(new SmsCreditResponse(IsTestMode: false, RemainingToman: null, CreditUsedUp: false));
    }

    // ---- Helpers ----

    private async Task<SmsCreditResponse> GetAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<SmsCreditResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>A birthday SMS for a new member, sent or failed at the given moment.</summary>
    private async Task AddAsync(DateTimeOffset? sentAt = null, DateTimeOffset? failedAt = null, int? code = null)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("سارا محمدی", $"+98915{suffix:D7}");
        var notification = Notification.ForBirthday(member.Id, 1405, member.PhoneNumber, "gymBirthday", new SmsTokens("۱۴۰۵"));
        if (sentAt is { } sent)
        {
            notification.MarkSent(suffix, 1_350m, sent);
        }
        else if (failedAt is { } failed)
        {
            notification.MarkFailed(code, failed);
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static DateTimeOffset Moment(int hourUtc) => new(2026, 10, 8, hourUtc, 0, 0, TimeSpan.Zero);
}

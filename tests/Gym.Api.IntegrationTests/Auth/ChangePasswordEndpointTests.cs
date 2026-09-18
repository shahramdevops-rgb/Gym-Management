using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Auth;
using Gym.Application.Auth.GetCurrentUser;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// The forced password change gate, <c>POST /api/auth/change-password</c> and <c>GET /api/auth/me</c>.
/// The rules under test are BUSINESS_RULES.md §1.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ChangePasswordEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ChangePasswordPath = "/api/auth/change-password";
    private const string MePath = "/api/auth/me";
    private const string NewPassword = "Chosen5678";

    [Fact]
    public async Task Me_UserWhoMustChangePassword_Returns403PasswordChangeRequired()
    {
        await TestUsers.CreateAsync(Fixture, role: Roles.Owner);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await GetMeAsync(client, accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.PasswordChangeRequired");
    }

    [Fact]
    public async Task ChangePassword_UserWhoMustChangePassword_SucceedsAndTheNewTokenPassesTheGate()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var change = await ChangePasswordAsync(client, accessToken, TestUsers.Password, NewPassword);

        change.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = (await change.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        session.MustChangePassword.ShouldBeFalse();
        change.GetRefreshCookie().ShouldNotBeNull();

        using var me = await GetMeAsync(client, session.AccessToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await me.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        body.Id.ShouldBe(user.Id);
        body.UserName.ShouldBe("staff");
        body.Roles.ShouldBe([Roles.Staff]);
        body.MustChangePassword.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangePassword_Succeeds_OnlyTheNewPasswordLogsIn()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var change = await ChangePasswordAsync(client, accessToken, TestUsers.Password, NewPassword);

        using var withOld = await client.LoginAsync("staff", TestUsers.Password);
        withOld.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var withNew = await client.LoginAsync("staff", NewPassword);
        withNew.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_RevokesEveryOtherSession()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var thisBrowser = await client.LoginAsync("staff", TestUsers.Password);
        using var otherBrowser = await client.LoginAsync("staff", TestUsers.Password);
        var accessToken = (await thisBrowser.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))!
            .AccessToken;

        using var change = await ChangePasswordAsync(client, accessToken, TestUsers.Password, NewPassword);

        using var otherRefresh = await client.RefreshAsync(otherBrowser.ReadRefreshToken());
        otherRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var oldRefresh = await client.RefreshAsync(thisBrowser.ReadRefreshToken());
        oldRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The session handed back by the change is the one that keeps working.
        using var newRefresh = await client.RefreshAsync(change.ReadRefreshToken());
        newRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tokens = await LoadTokensAsync();
        tokens.Count(t => t.RevokedReason == RefreshTokenRevocationReason.PasswordChanged).ShouldBe(2);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_StampsTheNewRefreshTokenWithTheUserWhoChangedIt()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var change = await ChangePasswordAsync(client, accessToken, TestUsers.Password, NewPassword);

        // ICurrentUser end to end: an authenticated request's user reaches the audit fields.
        var created = (await LoadTokensAsync()).Single(t => t.TokenHash == RefreshTokenSecret.Hash(change.ReadRefreshToken()));
        created.CreatedBy.ShouldBe(user.Id);

        // Login is anonymous, so the token it created was made on nobody's behalf.
        (await LoadTokensAsync()).Single(t => t.RevokedReason == RefreshTokenRevocationReason.PasswordChanged)
            .CreatedBy.ShouldBeNull();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400AndCountsTowardLockout()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await ChangePasswordAsync(client, accessToken, "Wrong1234", NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.CurrentPasswordIncorrect");
        (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_Returns400PasswordUnchanged()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await ChangePasswordAsync(client, accessToken, TestUsers.Password, TestUsers.Password);

        // Otherwise the forced change could be skipped by typing the temporary password twice.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.PasswordUnchanged");
    }

    [Theory]
    [InlineData("abc1", "Auth.PasswordTooShort")]
    [InlineData("onlyletters", "Auth.PasswordRequiresLetterAndDigit")]
    [InlineData("12345678", "Auth.PasswordRequiresLetterAndDigit")]
    public async Task ChangePassword_WeakNewPassword_Returns400WithFieldCode(string newPassword, string expectedCode)
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await ChangePasswordAsync(client, accessToken, TestUsers.Password, newPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("newPassword")[0].GetProperty("code").GetString()
            .ShouldBe(expectedCode);
    }

    [Fact]
    public async Task ChangePassword_PersianLettersAndDigits_AreAccepted()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        // Typed on a Persian keyboard: Persian letters and Persian digits both count.
        using var response = await ChangePasswordAsync(client, accessToken, TestUsers.Password, "رمزعبور۱۲۳۴");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401ProblemDetails()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            ChangePasswordPath,
            new { currentPassword = TestUsers.Password, newPassword = NewPassword },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Unauthenticated");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401ProblemDetailsWithBearerChallenge()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Unauthenticated");
    }

    [Fact]
    public async Task Me_UserWithOwnPassword_ReturnsTheCurrentUser()
    {
        var user = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);

        using var response = await GetMeAsync(client, accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        body.Id.ShouldBe(user.Id);
        body.Roles.ShouldBe([Roles.Owner]);
    }

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken) =>
        client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, MePath).WithBearer(accessToken),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> ChangePasswordAsync(
        HttpClient client,
        string accessToken,
        string currentPassword,
        string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ChangePasswordPath)
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };

        return client.SendAsync(request.WithBearer(accessToken), TestContext.Current.CancellationToken);
    }

    private async Task<List<RefreshToken>> LoadTokensAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.RefreshTokens.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }
}

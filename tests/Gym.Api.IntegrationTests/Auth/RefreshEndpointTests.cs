using System.Net;
using System.Net.Http.Json;

using Gym.Api.Common;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Auth;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// Refresh token issue, rotation, reuse detection and logout, end to end.
/// The rules under test are BUSINESS_RULES.md §1.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RefreshEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Login_Succeeds_SetsHttpOnlySecureStrictRefreshCookie()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", TestUsers.Password);

        var cookie = response.GetRefreshCookie().ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue("JavaScript must not be able to read the refresh token.");
        cookie.Secure.ShouldBeTrue();
        cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        cookie.Path.Value.ShouldBe(RefreshTokenCookie.Path);

        var expires = cookie.Expires.ShouldNotBeNull();
        (expires - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task Login_Succeeds_KeepsTheRefreshTokenOutOfTheResponseBody()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", TestUsers.Password);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(response.ReadRefreshToken());
    }

    [Fact]
    public async Task Login_Succeeds_StoresOnlyTheHashOfTheRefreshToken()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", TestUsers.Password);
        var refreshToken = response.ReadRefreshToken();

        var stored = (await LoadTokensAsync()).ShouldHaveSingleItem();
        stored.TokenHash.ShouldNotBe(refreshToken);
        stored.TokenHash.ShouldBe(RefreshTokenSecret.Hash(refreshToken));
    }

    [Fact]
    public async Task Refresh_ValidCookie_ReturnsNewAccessTokenAndRotatesTheCookie()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        var original = login.ReadRefreshToken();

        using var response = await client.RefreshAsync(original);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        new JsonWebToken(body.AccessToken).Subject.ShouldBe(user.Id.ToString());

        var rotated = response.ReadRefreshToken();
        rotated.ShouldNotBe(original);

        // The parent is used up and points at its child; both are in one family.
        var tokens = await LoadTokensAsync();
        tokens.Count.ShouldBe(2);
        var parent = tokens.Single(t => t.TokenHash == RefreshTokenSecret.Hash(original));
        var child = tokens.Single(t => t.TokenHash == RefreshTokenSecret.Hash(rotated));
        parent.WasRotated.ShouldBeTrue();
        parent.ReplacedByTokenId.ShouldBe(child.Id);
        child.FamilyId.ShouldBe(parent.FamilyId);
        child.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_RotatedTokenChain_KeepsWorking()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        var token = login.ReadRefreshToken();

        for (var round = 1; round <= 3; round++)
        {
            using var response = await client.RefreshAsync(token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"round {round}");
            token = response.ReadRefreshToken();
        }
    }

    [Fact]
    public async Task Refresh_ReusedRotatedToken_Returns401AndRevokesTheWholeFamily()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        var stolen = login.ReadRefreshToken();

        // The real user refreshes: the stolen copy is now a rotated token.
        using var legitimate = await client.RefreshAsync(stolen);
        var current = legitimate.ReadRefreshToken();

        // The thief presents the copy.
        using var reuse = await client.RefreshAsync(stolen);

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await reuse.ReadErrorCodeAsync()).ShouldBe("Auth.RefreshTokenInvalid");

        // The newest token in the family, which nobody reused, has stopped working too.
        using var afterwards = await client.RefreshAsync(current);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var tokens = await LoadTokensAsync();
        tokens.ShouldAllBe(t => t.IsRevoked);
        tokens.Single(t => t.TokenHash == RefreshTokenSecret.Hash(current))
            .RevokedReason.ShouldBe(RefreshTokenRevocationReason.ReuseDetected);
    }

    [Fact]
    public async Task Refresh_ReuseInOneFamily_LeavesOtherLoginsAlone()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var firstLogin = await client.LoginAsync("staff", TestUsers.Password);
        using var secondLogin = await client.LoginAsync("staff", TestUsers.Password);
        var first = firstLogin.ReadRefreshToken();

        using var rotate = await client.RefreshAsync(first);
        using var reuse = await client.RefreshAsync(first);

        // A separate login (another computer at the desk) is a separate family.
        using var other = await client.RefreshAsync(secondLogin.ReadRefreshToken());
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401AndClearsTheCookie()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.RefreshAsync(refreshToken: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.RefreshTokenInvalid");
        response.GetRefreshCookie().ShouldNotBeNull().Expires.ShouldNotBeNull().ShouldBeLessThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.RefreshAsync(RefreshTokenSecret.Generate());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.RefreshTokenInvalid");
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        await ExecuteSqlAsync("UPDATE refresh_tokens SET expires_at = now() - interval '1 minute'");

        using var response = await client.RefreshAsync(login.ReadRefreshToken());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.RefreshTokenInvalid");
    }

    [Fact]
    public async Task Refresh_InactiveUser_Returns401UserInactiveAndRevokesTheFamily()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        await TestUsers.DeactivateAsync(Fixture, user.Id);

        using var response = await client.RefreshAsync(login.ReadRefreshToken());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.UserInactive");
        (await LoadTokensAsync()).ShouldAllBe(t => t.RevokedReason == RefreshTokenRevocationReason.UserInactive);
    }

    [Fact]
    public async Task Refresh_AfterFlagChangeInDatabase_NewAccessTokenReflectsTheDatabase()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        await ExecuteSqlAsync("UPDATE users SET must_change_password = false");

        using var response = await client.RefreshAsync(login.ReadRefreshToken());

        // Re-read from the database, not copied from the old token: once task 1.4 lets the
        // user change their password, the next refresh must stop sending them back to it.
        var body = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        body.MustChangePassword.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_LockedOutUser_StillSucceeds()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        await ExecuteSqlAsync("UPDATE users SET lockout_end = now() + interval '15 minutes'");

        using var response = await client.RefreshAsync(login.ReadRefreshToken());

        // Someone else mistyping this user's password must not log the real user out.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_ValidCookie_RevokesTheTokenAndClearsTheCookie()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        var refreshToken = login.ReadRefreshToken();

        using var logout = await client.LogoutAsync(refreshToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cleared = logout.GetRefreshCookie().ShouldNotBeNull();
        cleared.Path.Value.ShouldBe(RefreshTokenCookie.Path);
        cleared.Expires.ShouldNotBeNull().ShouldBeLessThan(DateTimeOffset.UtcNow);

        (await LoadTokensAsync()).ShouldHaveSingleItem().RevokedReason.ShouldBe(RefreshTokenRevocationReason.Logout);

        using var refresh = await client.RefreshAsync(refreshToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204AndClearsTheCookie()
    {
        using var client = Fixture.CreateClient();

        using var logout = await client.LogoutAsync(refreshToken: null);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logout.GetRefreshCookie().ShouldNotBeNull();
    }

    [Fact]
    public async Task Logout_UnknownToken_Returns204()
    {
        using var client = Fixture.CreateClient();

        using var logout = await client.LogoutAsync(RefreshTokenSecret.Generate());

        // The same answer as for a real token, so logout cannot be used to test guesses.
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<List<RefreshToken>> LoadTokensAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.RefreshTokens.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }
}

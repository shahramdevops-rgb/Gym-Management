using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Auth;
using Gym.Infrastructure.Identity;

using Microsoft.IdentityModel.JsonWebTokens;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> end to end: HTTP, validation filter, handler, Identity and Postgres.
/// The rules under test are BUSINESS_RULES.md §1.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LoginEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string WrongPassword = "Wrong1234";

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithAccessToken()
    {
        var user = await TestUsers.CreateAsync(Fixture, userName: "owner", role: Roles.Owner);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("owner", TestUsers.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();

        var token = new JsonWebToken(body.AccessToken);
        token.Subject.ShouldBe(user.Id.ToString());
        token.GetClaim(JwtClaimNames.Name).Value.ShouldBe("owner");
        token.GetClaim(JwtClaimNames.Role).Value.ShouldBe(Roles.Owner);
        token.GetClaim(JwtClaimNames.MustChangePassword).Value.ShouldBe("true");
        body.MustChangePassword.ShouldBeTrue();

        // BUSINESS_RULES.md §1: 15 minutes. Compared as whole seconds, which is what a JWT stores.
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromMinutes(15));
        body.ExpiresAt.ToUnixTimeSeconds().ShouldBe(new DateTimeOffset(token.ValidTo).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Login_UserNameInDifferentCase_Succeeds()
    {
        await TestUsers.CreateAsync(Fixture, userName: "staff");
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("STAFF", TestUsers.Password);

        // Identity compares normalized (upper-cased) names, so staff do not have to remember
        // how they capitalized their user name.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401InvalidCredentials()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("nobody", TestUsers.Password);

        // The same code as a wrong password, so a caller cannot tell which user names exist.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksOutEvenWithTheCorrectPassword()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await client.LoginAsync("staff", WrongPassword);
            (await wrong.ReadErrorCodeAsync()).ShouldBe("Auth.InvalidCredentials", $"attempt {attempt}");
        }

        // The fifth failure reaches the limit and already reports the lockout.
        using var fifth = await client.LoginAsync("staff", WrongPassword);
        (await fifth.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");

        using var correct = await client.LoginAsync("staff", TestUsers.Password);
        correct.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await correct.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsTheFailedAttemptCount()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await client.LoginAsync("staff", WrongPassword);
        }

        using var success = await client.LoginAsync("staff", TestUsers.Password);
        success.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(0);

        // "5 consecutive": after the reset, one more wrong password is the first of a new run,
        // not the fifth of the old one.
        using var afterReset = await client.LoginAsync("staff", WrongPassword);
        (await afterReset.ReadErrorCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_InactiveUserWithCorrectPassword_Returns401UserInactive()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        await TestUsers.DeactivateAsync(Fixture, user.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", TestUsers.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.UserInactive");
    }

    [Fact]
    public async Task Login_InactiveUserWithWrongPassword_Returns401InvalidCredentials()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        await TestUsers.DeactivateAsync(Fixture, user.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", WrongPassword);

        // Only someone who knows the password learns that the account is inactive.
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400WithAFieldErrorForEach()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("", "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().ShouldBe("General.ValidationFailed");

        var errors = body.RootElement.GetProperty("errors");
        errors.GetProperty("userName")[0].GetProperty("code").GetString().ShouldBe("Auth.UserNameRequired");
        errors.GetProperty("password")[0].GetProperty("code").GetString().ShouldBe("Auth.PasswordRequired");
    }
}

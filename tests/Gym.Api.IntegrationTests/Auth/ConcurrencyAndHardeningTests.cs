using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Identity;

using Microsoft.Extensions.Configuration;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// Findings of the Phase 1 review: races that used to undercount lockout or answer 500, and
/// rules that were enforced only by the browser.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ConcurrencyAndHardeningTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string WrongPassword = "Wrong1234";

    [Fact]
    public async Task Login_ParallelWrongPasswords_EveryAttemptCountsAndNoneFails()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        // Eight guesses at the same moment. With a read-add-save counter they all read the
        // same count, one save wins, and the rest answered 500 without counting.
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.LoginAsync("staff", WrongPassword)));

        try
        {
            responses.Select(response => response.StatusCode).ShouldAllBe(status => status == HttpStatusCode.Unauthorized);

            // At least five failures were counted, so the account is locked: the right
            // password is refused.
            using var correct = await client.LoginAsync("staff", TestUsers.Password);
            (await correct.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Login_ParallelCorrectPasswordsAfterFailures_AllSucceed()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using (var wrong = await client.LoginAsync("staff", WrongPassword))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Each success resets the failed count; the resets must not trip over each other.
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => client.LoginAsync("staff", TestUsers.Password)));

        responses.Select(response => response.StatusCode).ShouldAllBe(status => status == HttpStatusCode.OK);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Refresh_SameTokenInParallel_NeverAnswers500()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        var token = login.ReadRefreshToken();

        // Five tabs refreshing with one cookie at once: at most one may win; the rest are
        // reuse. None of it is an unexpected failure.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.RefreshAsync(token)));

        responses.Select(response => response.StatusCode)
            .ShouldAllBe(status => status == HttpStatusCode.OK || status == HttpStatusCode.Unauthorized);
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task ChangePassword_FiveWrongCurrentPasswords_LocksTheAccount()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        string? lastCode = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await ChangePasswordAsync(client, accessToken, WrongPassword, "Chosen5678");
            lastCode = await response.ReadErrorCodeAsync();
        }

        lastCode.ShouldBe("Auth.LockedOut");
        using var login = await client.LoginAsync("staff", TestUsers.Password);
        (await login.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");
    }

    [Fact]
    public async Task ChangePassword_UserDeactivatedWhileTheirTokenIsValid_IsRefused()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        var accessToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        await TestUsers.DeactivateAsync(Fixture, user.Id);

        using var response = await ChangePasswordAsync(client, accessToken, TestUsers.Password, "Chosen5678");

        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.UserInactive");
    }

    [Fact]
    public async Task Login_PasswordTypedWithPersianDigits_MatchesTheSamePasswordWithEnglishDigits()
    {
        // TestUsers.Password is "Staff1234". The server converts digits itself, so a client
        // other than the web app cannot end up with a different password from the same keys.
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginAsync("staff", "Staff۱۲۳۴");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Seeding_PasswordWithPersianDigits_LogsInWithEnglishDigits()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:OwnerUserName"] = "owner",
                ["Seed:OwnerPassword"] = "Owner۱۲۳۴",
            })
            .Build();

        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);

        using var client = Fixture.CreateClient();
        using var response = await client.LoginAsync("owner", "Owner1234");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> ChangePasswordAsync(
        HttpClient client,
        string accessToken,
        string currentPassword,
        string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };

        return client.SendAsync(request.WithBearer(accessToken), TestContext.Current.CancellationToken);
    }
}

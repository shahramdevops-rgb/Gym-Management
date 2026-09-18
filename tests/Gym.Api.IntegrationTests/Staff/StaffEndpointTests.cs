using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Staff;
using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Staff;

/// <summary>
/// <c>/api/staff</c>: the Owner creates, lists, deactivates, reactivates and resets staff
/// accounts. The rules under test are BUSINESS_RULES.md §1.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class StaffEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string StaffPath = "/api/staff";
    private const string TemporaryPassword = "Temp1234";

    [Fact]
    public async Task CreateStaff_AsStaff_Returns403Forbidden()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff");
        using var client = Fixture.CreateClient();
        var staffToken = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);

        using var response = await SendAsync(client, staffToken, HttpMethod.Post, StaffPath, NewStaff("another"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Forbidden");
        (await CountUsersAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateStaff_AsOwner_Returns201AndTheAccountCanLogInWithTheTemporaryPassword()
    {
        var (client, ownerToken) = await OwnerClientAsync();

        using var response = await SendAsync(client, ownerToken, HttpMethod.Post, StaffPath, NewStaff("reza", "  رضا   احمدی "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var staff = (await response.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{StaffPath}/{staff.Id}");
        staff.UserName.ShouldBe("reza");
        staff.FullName.ShouldBe("رضا احمدی", "the name is trimmed and repeated spaces collapsed.");
        staff.IsActive.ShouldBeTrue();
        staff.MustChangePassword.ShouldBeTrue();

        using var login = await client.LoginAsync("reza", TemporaryPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateStaff_ExistingUserNameInAnotherCase_Returns409()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        using var first = await SendAsync(client, ownerToken, HttpMethod.Post, StaffPath, NewStaff("reza"));

        using var second = await SendAsync(client, ownerToken, HttpMethod.Post, StaffPath, NewStaff("REZA"));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadErrorCodeAsync()).ShouldBe("Staff.UserNameTaken");
    }

    [Theory]
    [InlineData("ab", "رضا", TemporaryPassword, "userName", "Staff.UserNameLength")]
    [InlineData("رضا", "رضا", TemporaryPassword, "userName", "Staff.UserNameInvalidCharacters")]
    [InlineData("reza", "   ", TemporaryPassword, "fullName", "Staff.FullNameRequired")]
    [InlineData("reza", "رضا", "short1", "temporaryPassword", "Auth.PasswordTooShort")]
    [InlineData("reza", "رضا", "noDigitsHere", "temporaryPassword", "Auth.PasswordRequiresLetterAndDigit")]
    public async Task CreateStaff_InvalidInput_Returns400WithFieldCode(
        string userName,
        string fullName,
        string password,
        string field,
        string expectedCode)
    {
        var (client, ownerToken) = await OwnerClientAsync();

        using var response = await SendAsync(
            client, ownerToken, HttpMethod.Post, StaffPath,
            new { userName, fullName, temporaryPassword = password });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FirstFieldErrorAsync(response, field)).ShouldBe(expectedCode);
    }

    [Fact]
    public async Task ListStaff_AsOwner_ReturnsStaffOnlyOrderedByNameAndPaged()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        foreach (var (userName, fullName) in new[] { ("sara", "سارا"), ("ali", "علی"), ("bahar", "بهار") })
        {
            using var created = await SendAsync(client, ownerToken, HttpMethod.Post, StaffPath, NewStaff(userName, fullName));
            created.EnsureSuccessStatusCode();
        }

        using var response = await SendAsync(client, ownerToken, HttpMethod.Get, $"{StaffPath}?page=1&pageSize=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<StaffResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();

        // The Owner is not a staff account and does not appear.
        page.TotalCount.ShouldBe(3);
        page.Items.Select(staff => staff.FullName).ShouldBe(["بهار", "سارا"]);
    }

    [Fact]
    public async Task ListStaff_PageSizeOver100_Returns400()
    {
        var (client, ownerToken) = await OwnerClientAsync();

        using var response = await SendAsync(client, ownerToken, HttpMethod.Get, $"{StaffPath}?pageSize=101");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FirstFieldErrorAsync(response, "pageSize")).ShouldBe("Paging.PageSizeInvalid");
    }

    [Fact]
    public async Task Deactivate_LoggedInStaff_CannotRefreshOrLogIn()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");
        using var staffLogin = await client.LoginAsync("reza", TemporaryPassword);

        using var deactivate = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/deactivate");

        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deactivate.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken))!.IsActive.ShouldBeFalse();

        // Checked before any refresh: refresh would also revoke an inactive user's family, so
        // only this proves that deactivation itself revoked them, immediately.
        var tokens = await LoadTokensAsync(staff.Id);
        tokens.ShouldNotBeEmpty();
        tokens.ShouldAllBe(token => token.RevokedReason == RefreshTokenRevocationReason.UserInactive);

        using var refresh = await client.RefreshAsync(staffLogin.ReadRefreshToken());
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var login = await client.LoginAsync("reza", TemporaryPassword);
        (await login.ReadErrorCodeAsync()).ShouldBe("Auth.UserInactive");
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_SucceedsAndStaysInactive()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");
        using var first = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/deactivate");

        using var second = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/deactivate");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken))!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Reactivate_DeactivatedStaff_CanLogInWithTheSamePassword()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");
        using var deactivate = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/deactivate");

        using var reactivate = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/reactivate");

        reactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var login = await client.LoginAsync("reza", TemporaryPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_LockedOutStaff_UnlocksRequiresChangeAndEndsSessions()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");
        using var session = await client.LoginAsync("reza", TemporaryPassword);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await client.LoginAsync("reza", "Wrong1234");
        }

        using var reset = await SendAsync(
            client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/reset-password",
            new { temporaryPassword = "Fresh5678" });

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var oldPassword = await client.LoginAsync("reza", TemporaryPassword);
        oldPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var newPassword = await client.LoginAsync("reza", "Fresh5678");
        newPassword.StatusCode.ShouldBe(HttpStatusCode.OK, "the reset clears the lockout.");
        (await newPassword.Content.ReadFromJsonAsync<Application.Auth.AccessTokenResponse>(TestContext.Current.CancellationToken))!
            .MustChangePassword.ShouldBeTrue();

        using var oldSession = await client.RefreshAsync(session.ReadRefreshToken());
        oldSession.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400AndKeepsTheOldPassword()
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");

        using var reset = await SendAsync(
            client, ownerToken, HttpMethod.Post, $"{StaffPath}/{staff.Id}/reset-password",
            new { temporaryPassword = "weak" });

        reset.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var login = await client.LoginAsync("reza", TemporaryPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task StaffActions_OnTheOwnersOwnAccount_Return404(string action)
    {
        var (client, ownerToken) = await OwnerClientAsync();
        var ownerId = await UserIdAsync("owner");

        using var response = await SendAsync(client, ownerToken, HttpMethod.Post, $"{StaffPath}/{ownerId}/{action}");

        // The Owner cannot lock themselves out through the staff screen.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Staff.NotFound");
    }

    [Fact]
    public async Task GetStaff_UnknownId_Returns404()
    {
        var (client, ownerToken) = await OwnerClientAsync();

        using var response = await SendAsync(client, ownerToken, HttpMethod.Get, $"{StaffPath}/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListStaff_OwnerWithTemporaryPassword_Returns403PasswordChangeRequired()
    {
        await TestUsers.CreateAsync(Fixture, userName: "owner", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        var ownerToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);

        using var response = await SendAsync(client, ownerToken, HttpMethod.Get, StaffPath);

        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.PasswordChangeRequired");
    }

    private async Task<(HttpClient Client, string OwnerToken)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private static async Task<StaffResponse> CreateStaffAsync(HttpClient client, string ownerToken, string userName)
    {
        using var response = await SendAsync(client, ownerToken, HttpMethod.Post, StaffPath, NewStaff(userName));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static object NewStaff(string userName, string fullName = "کارمند") =>
        new { userName, fullName, temporaryPassword = TemporaryPassword };

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string accessToken,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(accessToken), TestContext.Current.CancellationToken);
    }

    private static async Task<string?> FirstFieldErrorAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private async Task<int> CountUsersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Where(user => user.UserName == userName)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<RefreshToken>> LoadTokensAsync(Guid userId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}

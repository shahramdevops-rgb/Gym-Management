using System.Net;
using System.Net.Http.Json;

using Gym.Api.Admin;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Auth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Admin;

/// <summary>
/// The server console (<c>./server.sh unlock | set-password | rename</c>), run in process against
/// the real database: BUSINESS_RULES.md §1 *Lockout*.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AdminConsoleTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string NewPassword = "server kettle 2468";

    [Fact]
    public async Task Unlock_LockedOutOwner_CanLogInAgain()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "boss", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginAsync("boss", "Wrong1234");
        }

        var (exitCode, output) = await RunAsync("unlock", "boss");

        exitCode.ShouldBe(0, output);
        using var login = await client.LoginAsync("boss", TestUsers.Password);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetPassword_FromStandardInput_SetsItEndsSessionsAndNeedsNoChange()
    {
        var owner = await TestUsers.CreateAsync(Fixture, userName: "boss", role: Roles.Owner);
        using var client = Fixture.CreateClient();
        using var oldSession = await client.LoginAsync("boss", TestUsers.Password);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginAsync("boss", "Wrong1234");
        }

        var (exitCode, output) = await RunAsync(["set-password", "boss"], input: NewPassword + "\n");

        exitCode.ShouldBe(0, output);
        using var login = await client.LoginAsync("boss", NewPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK, "the lockout is cleared too.");
        (await login.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))!
            .MustChangePassword.ShouldBeFalse("they typed it themselves.");
        using var refresh = await client.RefreshAsync(oldSession.ReadRefreshToken());
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "every old session ends.");
        (await CountDevicesAsync(owner.Id)).ShouldBe(1, "only the device that just logged in with the new password.");
    }

    [Fact]
    public async Task SetPassword_PasswordAgainstThePolicy_IsRefusedAndTheOldOneStays()
    {
        await TestUsers.CreateAsync(Fixture, userName: "boss", role: Roles.Owner);

        var (exitCode, output) = await RunAsync(["set-password", "boss"], input: "Football2024!\n");

        exitCode.ShouldBe(1);
        output.ShouldContain("Auth.PasswordTooCommon");
        using var client = Fixture.CreateClient();
        using var login = await client.LoginAsync("boss", TestUsers.Password);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetPassword_NothingOnStandardInput_ReturnsUsageError()
    {
        await TestUsers.CreateAsync(Fixture, userName: "boss", role: Roles.Owner);

        var (exitCode, _) = await RunAsync(["set-password", "boss"], input: "");

        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task Rename_GuessableOwnerName_LogsInWithTheNewNameOnly()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "Owner", role: Roles.Owner);

        var (exitCode, output) = await RunAsync("rename", "Owner", "bahram.k");

        exitCode.ShouldBe(0, output);
        using var client = Fixture.CreateClient();
        using var newName = await client.LoginAsync("bahram.k", TestUsers.Password);
        newName.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var oldName = await client.LoginAsync("Owner", TestUsers.Password);
        oldName.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("admin", "Accounts.UserNameGuessable")]
    [InlineData("مدیر", "Accounts.UserNameInvalid")]
    [InlineData("ab", "Accounts.UserNameInvalid")]
    public async Task Rename_BadNewName_IsRefused(string newUserName, string expectedCode)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "Owner", role: Roles.Owner);

        var (exitCode, output) = await RunAsync("rename", "Owner", newUserName);

        exitCode.ShouldBe(1);
        output.ShouldContain(expectedCode);
    }

    [Fact]
    public async Task Rename_NameAnotherAccountUses_IsRefused()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "Owner", role: Roles.Owner);
        await TestUsers.CreateAsync(Fixture, userName: "sara");

        var (exitCode, output) = await RunAsync("rename", "Owner", "SARA");

        exitCode.ShouldBe(1);
        output.ShouldContain("Accounts.UserNameTaken");
    }

    [Fact]
    public async Task Unlock_UnknownUser_IsRefused()
    {
        var (exitCode, output) = await RunAsync("unlock", "nobody");

        exitCode.ShouldBe(1);
        output.ShouldContain("Accounts.UserNotFound");
    }

    [Theory]
    [InlineData]
    [InlineData("unlock")]
    [InlineData("delete", "boss")]
    public async Task Run_UnknownOrIncompleteCommand_PrintsUsage(params string[] args)
    {
        var (exitCode, output) = await RunAsync(args);

        exitCode.ShouldBe(2);
        output.ShouldContain("usage:");
    }

    private Task<(int ExitCode, string Output)> RunAsync(params string[] args) => RunAsync(args, input: "");

    private async Task<(int ExitCode, string Output)> RunAsync(string[] args, string input)
    {
        using var reader = new StringReader(input);
        await using var writer = new StringWriter();

        var exitCode = await AdminConsole.RunAsync(Fixture.Services, args, reader, writer, TestContext.Current.CancellationToken);

        return (exitCode, writer.ToString());
    }

    private async Task<int> CountDevicesAsync(Guid userId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.TrustedDevices.CountAsync(device => device.UserId == userId, TestContext.Current.CancellationToken);
    }
}

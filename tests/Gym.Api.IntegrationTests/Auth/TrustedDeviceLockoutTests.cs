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

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// BUSINESS_RULES.md §1 *Lockout* and ADR 0004: wrong passwords from unknown devices lock only
/// unknown devices, and a trusted device has its own count.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TrustedDeviceLockoutTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string WrongPassword = "Wrong1234";

    [Fact]
    public async Task Login_Succeeds_SetsAHttpOnlySecureStrictDeviceCookieAndTrustsTheDevice()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();

        using var response = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.GetDeviceCookie().ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Secure.ShouldBeTrue();
        cookie.SameSite.ShouldBe(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
        cookie.Path.Value.ShouldBe(RefreshTokenCookie.Path);
        cookie.Expires.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(89));

        var device = (await LoadDevicesAsync()).ShouldHaveSingleItem();
        device.UserId.ShouldBe(user.Id);
        device.TokenHash.ShouldBe(RefreshTokenSecret.Hash(cookie.Value.Value!));
    }

    [Fact]
    public async Task Login_FailedAttempt_LeavesTheDeviceCookieAlone()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var first = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);

        using var wrong = await client.LoginFromDeviceAsync("staff", WrongPassword, first.ReadDeviceToken());

        // A typo at the front desk must not turn the front-desk PC into an unknown device.
        wrong.GetDeviceCookie().ShouldBeNull();
    }

    [Fact]
    public async Task Login_AttackerLocksOutUnknownDevices_TrustedDeviceStillLogsIn()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var frontDesk = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var frontDeskDevice = frontDesk.ReadDeviceToken();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginFromDeviceAsync("staff", WrongPassword, deviceToken: null);
        }

        using var attackerWithRightPassword = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        attackerWithRightPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await attackerWithRightPassword.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");

        using var frontDeskAgain = await client.LoginFromDeviceAsync("staff", TestUsers.Password, frontDeskDevice);
        frontDeskAgain.StatusCode.ShouldBe(HttpStatusCode.OK, "the attacker's lockout does not reach a trusted device.");
    }

    [Fact]
    public async Task Login_WrongPasswordsOnATrustedDevice_LockOnlyThatDevice()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var first = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = first.ReadDeviceToken();

        HttpStatusCode lastStatus = default;
        string? lastCode = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await client.LoginFromDeviceAsync("staff", WrongPassword, device);
            lastStatus = wrong.StatusCode;
            lastCode = await wrong.ReadErrorCodeAsync();
        }

        lastStatus.ShouldBe(HttpStatusCode.Unauthorized);
        lastCode.ShouldBe("Auth.LockedOut", "the fifth wrong password already reports the lockout.");

        using var sameDevice = await client.LoginFromDeviceAsync("staff", TestUsers.Password, device);
        (await sameDevice.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");

        using var otherDevice = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        otherDevice.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_CookieTheAppNeverIssued_IsNotTrustedAndIsReplaced()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        const string planted = "planted-by-someone-else";

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginFromDeviceAsync("staff", WrongPassword, planted);
        }

        using var locked = await client.LoginFromDeviceAsync("staff", TestUsers.Password, planted);
        (await locked.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut", "an unknown cookie is an unknown device.");

        await ClearUserLockoutAsync();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, planted);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        login.ReadDeviceToken().ShouldNotBe(planted, "a value the app never issued must not become trusted.");
    }

    [Fact]
    public async Task Login_TwoPeopleOnOnePc_ShareOneCookieWithOneRowEach()
    {
        var first = await TestUsers.CreateAsync(Fixture, userName: "sara");
        var second = await TestUsers.CreateAsync(Fixture, userName: "mina");
        using var client = Fixture.CreateClient();
        using var saraLogin = await client.LoginFromDeviceAsync("sara", TestUsers.Password, deviceToken: null);
        var pc = saraLogin.ReadDeviceToken();

        using var minaLogin = await client.LoginFromDeviceAsync("mina", TestUsers.Password, pc);

        // A new secret for the PC, and both rows moved onto it: still one cookie for the PC.
        var rotated = minaLogin.ReadDeviceToken();
        rotated.ShouldNotBe(pc);
        var devices = await LoadDevicesAsync();
        devices.Select(device => device.UserId).ShouldBe([first.Id, second.Id], ignoreOrder: true);
        devices.ShouldAllBe(device => device.TokenHash == RefreshTokenSecret.Hash(rotated));
    }

    [Fact]
    public async Task Login_OnATrustedDevice_RotatesTheSecretSoAnOldCopyIsAnUnknownDevice()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var first = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var copied = first.ReadDeviceToken();
        using var second = await client.LoginFromDeviceAsync("staff", TestUsers.Password, copied);
        var current = second.ReadDeviceToken();

        current.ShouldNotBe(copied);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var fromTheCopy = await client.LoginFromDeviceAsync("staff", WrongPassword, copied);
        }

        using var copyWithRightPassword = await client.LoginFromDeviceAsync("staff", TestUsers.Password, copied);
        (await copyWithRightPassword.ReadErrorCodeAsync()).ShouldBe(
            "Auth.LockedOut", "the copied secret no longer names a trusted device, so it counted at the unknown door.");
        using var realBrowser = await client.LoginFromDeviceAsync("staff", TestUsers.Password, current);
        realBrowser.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_TrustedDeviceAfterNinetyDays_IsAnUnknownDevice()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();
        await ExpireDevicesAsync();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await client.LoginFromDeviceAsync("staff", WrongPassword, device);
        }

        using var elsewhere = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        (await elsewhere.ReadErrorCodeAsync()).ShouldBe(
            "Auth.LockedOut", "an expired device counts at the unknown-devices door.");
    }

    [Fact]
    public async Task Login_ParallelWrongPasswordsOnATrustedDevice_EveryAttemptCountsThere()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.LoginFromDeviceAsync("staff", WrongPassword, device)));

        try
        {
            responses.Select(response => response.StatusCode).ShouldAllBe(status => status == HttpStatusCode.Unauthorized);
            using var correct = await client.LoginFromDeviceAsync("staff", TestUsers.Password, device);
            (await correct.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut");
            (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(0, "nothing counted at the unknown door.");
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
    public async Task Login_CorrectOnATrustedDevice_ResetsTheUnknownDoorsCountButNotItsLockout()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var guess = await client.LoginFromDeviceAsync("staff", WrongPassword, deviceToken: null);
        }

        using var atTheDesk = await client.LoginFromDeviceAsync("staff", TestUsers.Password, device);
        (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(0);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginFromDeviceAsync("staff", WrongPassword, deviceToken: null);
        }

        using var deskAgain = await client.LoginFromDeviceAsync("staff", TestUsers.Password, atTheDesk.ReadDeviceToken());
        deskAgain.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var elsewhere = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        (await elsewhere.ReadErrorCodeAsync()).ShouldBe("Auth.LockedOut", "a lockout already in force stays.");
    }

    [Fact]
    public async Task Login_WrongPasswordAfterTheDeviceWasForgotten_CountsAtTheUnknownDoor()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();
        await ForgetDevicesAsync();

        using var wrong = await client.LoginFromDeviceAsync("staff", WrongPassword, device);

        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_FromATrustedDevice_RenewsTheNinetyDaysAndRotatesTheSecret()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();
        await AgeDevicesAsync(TimeSpan.FromDays(89));

        using var refresh = await client.RefreshFromDeviceAsync(login.ReadRefreshToken(), device);

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = refresh.ReadDeviceToken();
        rotated.ShouldNotBe(device);
        var row = (await LoadDevicesAsync()).ShouldHaveSingleItem();
        row.TokenHash.ShouldBe(RefreshTokenSecret.Hash(rotated));
        row.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(89), "using the app counts as using the device.");
    }

    [Fact]
    public async Task Refresh_WithoutATrustedDevice_LeavesTheDeviceCookieAloneAndTrustsNothing()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        await ForgetDevicesAsync();

        using var refresh = await client.RefreshFromDeviceAsync(login.ReadRefreshToken(), login.ReadDeviceToken());

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        refresh.GetDeviceCookie().ShouldBeNull();
        (await LoadDevicesAsync()).ShouldBeEmpty("a refresh proves a session, not a password.");
    }

    [Fact]
    public async Task Login_DeviceTrustedForSomeoneElse_IsUnknownForThisUser()
    {
        await TestUsers.CreateAsync(Fixture, userName: "sara");
        await TestUsers.CreateAsync(Fixture, userName: "mina");
        using var client = Fixture.CreateClient();
        using var saraLogin = await client.LoginFromDeviceAsync("sara", TestUsers.Password, deviceToken: null);
        var pc = saraLogin.ReadDeviceToken();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var guess = await client.LoginFromDeviceAsync("mina", WrongPassword, pc);
        }

        using var minaFromElsewhere = await client.LoginFromDeviceAsync("mina", TestUsers.Password, deviceToken: null);
        (await minaFromElsewhere.ReadErrorCodeAsync()).ShouldBe(
            "Auth.LockedOut", "the PC is trusted for Sara, so Mina's wrong passwords there count at Mina's unknown-devices door.");
    }

    [Fact]
    public async Task ChangePassword_ForgetsEveryOtherDeviceAndKeepsThisOne()
    {
        await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var home = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        using var frontDesk = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var frontDeskDevice = frontDesk.ReadDeviceToken();
        var accessToken = (await frontDesk.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))!.AccessToken;

        using var change = await client.ChangePasswordFromDeviceAsync(
            accessToken, TestUsers.Password, "chosen kettle 5678", frontDeskDevice);

        change.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoadDevicesAsync()).ShouldHaveSingleItem().TokenHash.ShouldBe(RefreshTokenSecret.Hash(frontDeskDevice));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPasswordOnATrustedDevice_CountsOnThatDevice()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        using var client = Fixture.CreateClient();
        using var login = await client.LoginFromDeviceAsync("staff", TestUsers.Password, deviceToken: null);
        var device = login.ReadDeviceToken();
        var accessToken = (await login.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken))!.AccessToken;

        using var wrong = await client.ChangePasswordFromDeviceAsync(accessToken, WrongPassword, "chosen kettle 5678", device);

        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LoadDevicesAsync()).ShouldHaveSingleItem().FailedAttempts.ShouldBe(1);
        (await TestUsers.GetAccessFailedCountAsync(Fixture, user.Id)).ShouldBe(0);
    }

    private async Task<List<TrustedDevice>> LoadDevicesAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.TrustedDevices.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExpireDevicesAsync() => await AgeDevicesAsync(TimeSpan.FromDays(91));

    /// <summary>As if the last successful use on every device was <paramref name="age"/> ago.</summary>
    private async Task AgeDevicesAsync(TimeSpan age)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lastUsed = DateTimeOffset.UtcNow - age;
        var expires = lastUsed + TrustedDevice.Lifetime;

        await db.Database.ExecuteSqlAsync(
            $"UPDATE trusted_devices SET last_used_at = {lastUsed}, expires_at = {expires}",
            TestContext.Current.CancellationToken);
    }

    private async Task ForgetDevicesAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync($"DELETE FROM trusted_devices", TestContext.Current.CancellationToken);
    }

    private async Task ClearUserLockoutAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"UPDATE users SET lockout_end = NULL, access_failed_count = 0",
            TestContext.Current.CancellationToken);
    }
}

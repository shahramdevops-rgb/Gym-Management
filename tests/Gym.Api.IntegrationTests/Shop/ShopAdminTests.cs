using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Gym.Api.Authorization;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Shop;
using Gym.Application.Staff;
using Gym.Domain.Shop;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Shop;

/// <summary>
/// The shop admin, «مدیر فروشگاه» (ADR 0011, BUSINESS_RULES.md §16, task 16.2): made by the Owner on
/// «کارمندان», refused by every gym endpoint, and the only one who changes the shop's contact ways,
/// which the Owner reads.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed partial class ShopAdminTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ContactsPath = "/api/shop/contacts";
    private const string TemporaryPassword = "temp kettle 1234";

    [Fact]
    public async Task GymEndpoints_WithAShopAdminToken_AreAllRefused()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, "shop", Roles.Shop);
        using var client = Fixture.CreateClient();
        var shopToken = await client.LoginForAccessTokenAsync("shop", TestUsers.Password);

        var gymEndpoints = GymEndpoints();
        gymEndpoints.Count.ShouldBeGreaterThan(50, "the check below proves nothing if the gym's endpoints were not found.");

        var passed = new List<string>();
        foreach (var (method, path) in gymEndpoints)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path).WithBearer(shopToken);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                passed.Add($"{method} {path}: {(int)response.StatusCode}");
            }
        }

        passed.ShouldBeEmpty("a shop admin must reach no gym endpoint (ADR 0011).");
    }

    [Fact]
    public void Endpoints_AdmittingAShopAdmin_AreOnlyTheShopsAndTheAccounts()
    {
        // The walk above skips the endpoints whose policy admits a shop admin; this pins that list,
        // so a gym endpoint given one of those policies by mistake fails here.
        var admitting = Fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => AdmitsTheShopAdmin(data.Policy)))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToList();

        admitting.ShouldAllBe(endpoint =>
            endpoint == "GET /api/auth/me" ||
            endpoint == "POST /api/auth/change-password" ||
            endpoint.Contains(" /api/shop/", StringComparison.Ordinal));
        admitting.ShouldContain("GET /api/shop/contacts");
        admitting.ShouldContain("PUT /api/shop/contacts");
    }

    [Theory]
    [InlineData("\"Owner\"")]
    [InlineData("2")]
    public async Task CreateStaff_WithARoleTheOwnerCannotGive_Returns400AndMakesNoAccount(string role)
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/staff")
        {
            Content = new StringContent(
                $$"""{"userName":"other","fullName":"دیگری","temporaryPassword":"{{TemporaryPassword}}","role":{{role}}}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };

        using var response = await client.SendAsync(request.WithBearer(ownerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var scope = Fixture.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .AnyAsync(user => user.UserName == "other", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task ResetPasswordAndUnlock_AShopAdmin_WorkAsForStaff()
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);
        var shop = await TestUsers.CreateWithOwnPasswordAsync(Fixture, "shop", Roles.Shop);

        using var unlock = await SendAsync(client, ownerToken, HttpMethod.Post, $"/api/staff/{shop.Id}/unlock");
        using var reset = await SendAsync(client, ownerToken, HttpMethod.Post, $"/api/staff/{shop.Id}/reset-password", new
        {
            temporaryPassword = TemporaryPassword,
        });

        unlock.StatusCode.ShouldBe(HttpStatusCode.OK);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var login = await client.LoginAsync("shop", TemporaryPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TestUsers.GetMustChangePasswordAsync(Fixture, shop.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task GetMe_AsShopAdmin_ReturnsTheShopRole()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);

        using var response = await SendAsync(client, token, HttpMethod.Get, "/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();
        roles.ShouldBe(["Shop"]);
    }

    [Fact]
    public async Task CreateStaff_WithTheShopRole_MakesAShopAdminWhoReachesTheShopOnly()
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);

        using var created = await SendAsync(client, ownerToken, HttpMethod.Post, "/api/staff", new
        {
            userName = "maryam",
            fullName = "مریم فروشگاه",
            temporaryPassword = TemporaryPassword,
            role = "Shop",
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = (await created.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        account.Role.ShouldBe(StaffRole.Shop);
        account.MustChangePassword.ShouldBeTrue("a shop admin follows §1's password rules, as any staff account.");

        using var list = await SendAsync(client, ownerToken, HttpMethod.Get, "/api/staff");
        var listed = (await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("items");
        listed.EnumerateArray().Single().GetProperty("role").GetString().ShouldBe("Shop");

        // After the first password change, the shop's page opens and the member list does not.
        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
                $"UPDATE users SET must_change_password = false WHERE id = {account.Id}",
                TestContext.Current.CancellationToken);
        }

        var shopToken = await client.LoginForAccessTokenAsync("maryam", TemporaryPassword);
        using var contacts = await SendAsync(client, shopToken, HttpMethod.Get, ContactsPath);
        using var members = await SendAsync(client, shopToken, HttpMethod.Get, "/api/members");
        contacts.StatusCode.ShouldBe(HttpStatusCode.OK);
        members.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateStaff_WithoutARole_MakesAStaffMember()
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);

        using var created = await SendAsync(client, ownerToken, HttpMethod.Post, "/api/staff", new
        {
            userName = "reza",
            fullName = "رضا",
            temporaryPassword = TemporaryPassword,
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Role.ShouldBe(StaffRole.Staff);
    }

    [Fact]
    public async Task DeactivateStaff_AShopAdmin_StopsTheirLogin()
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);
        var shop = await TestUsers.CreateWithOwnPasswordAsync(Fixture, "shop", Roles.Shop);

        using var response = await SendAsync(client, ownerToken, HttpMethod.Post, $"/api/staff/{shop.Id}/deactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var login = await client.LoginAsync("shop", TestUsers.Password);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShopContacts_AsStaff_AreRefused()
    {
        var (client, staffToken) = await ClientAsync("staff", Roles.Staff);

        using var read = await SendAsync(client, staffToken, HttpMethod.Get, ContactsPath);
        using var write = await SendAsync(client, staffToken, HttpMethod.Put, ContactsPath, Contacts(version: 0));

        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShopContacts_AsOwner_AreReadButNotChanged()
    {
        var (client, ownerToken) = await ClientAsync("owner", Roles.Owner);

        using var read = await SendAsync(client, ownerToken, HttpMethod.Get, ContactsPath);
        var version = (await read.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Version;
        using var write = await SendAsync(client, ownerToken, HttpMethod.Put, ContactsPath, Contacts(version));

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "the Owner reads the shop but does not change it (§16).");
    }

    [Fact]
    public async Task UpdateShopContacts_AsShopAdmin_SavesThemCleanedAndTheOwnerReadsThem()
    {
        var (client, shopToken) = await ClientAsync("shop", Roles.Shop);
        var version = await VersionAsync(client, shopToken);

        using var response = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, new
        {
            mobilePhone = "۰۹۱۲ ۱۲۳ ۴۵۶۷",
            landlinePhone = " ۰۷۱-۳۲۳۴-۵۶۷۸ ",
            whatsAppPhone = "+989351234567",
            telegram = "@pasargad_gym",
            rubika = "pasargadgym",
            eitaa = "",
            bale = (string?)null,
            instagram = " pasargad.gym ",
            version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = (await response.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        saved.MobilePhone.ShouldBe("+989121234567");
        saved.LandlinePhone.ShouldBe("07132345678");
        saved.WhatsAppPhone.ShouldBe("+989351234567");
        saved.Telegram.ShouldBe("pasargad_gym", "the @ is dropped.");
        saved.Rubika.ShouldBe("pasargadgym");
        saved.Eitaa.ShouldBeNull("an empty way is not given.");
        saved.Bale.ShouldBeNull();
        saved.Instagram.ShouldBe("pasargad.gym");

        // A line break pasted after the number is dropped, not saved (it would break the database's check).
        using var again = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, new
        {
            landlinePhone = "07132345678\n",
            telegram = "pasargad_gym",
            version = saved.Version,
        });
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().LandlinePhone.ShouldBe("07132345678");

        await TestUsers.CreateWithOwnPasswordAsync(Fixture, "owner", Roles.Owner);
        var ownerToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        using var read = await SendAsync(client, ownerToken, HttpMethod.Get, ContactsPath);
        (await read.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Telegram.ShouldBe("pasargad_gym");
    }

    [Theory]
    [InlineData("landlinePhone", "7132345678", "landlinePhone", "Shop.LandlineInvalid")]
    [InlineData("landlinePhone", "0713234567\n", "landlinePhone", "Shop.LandlineInvalid")]
    [InlineData("telegram", "pasargad gym", "telegram", "Shop.HandleInvalid")]
    [InlineData("instagram", "https://instagram.com/gym", "instagram", "Shop.HandleInvalid")]
    public async Task UpdateShopContacts_AFieldOutOfShape_Returns400UnderThatField(string field, string value, string errorField, string code)
    {
        var (client, shopToken) = await ClientAsync("shop", Roles.Shop);
        var version = await VersionAsync(client, shopToken);

        using var response = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, new Dictionary<string, object?>
        {
            [field] = value,
            ["version"] = version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty(errorField)[0].GetProperty("code").GetString().ShouldBe(code);
    }

    [Theory]
    [InlineData("mobilePhone", "Shop.MobilePhoneInvalid")]
    [InlineData("whatsAppPhone", "Shop.WhatsAppPhoneInvalid")]
    public async Task UpdateShopContacts_AMobileThatIsNotIranian_Returns400WithItsCode(string field, string code)
    {
        var (client, shopToken) = await ClientAsync("shop", Roles.Shop);
        var version = await VersionAsync(client, shopToken);

        using var response = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, new Dictionary<string, object?>
        {
            [field] = "+14155550100",
            ["version"] = version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(code);
    }

    [Fact]
    public async Task UpdateShopContacts_OnAStaleVersion_Returns409AndKeepsTheOtherSave()
    {
        var (client, shopToken) = await ClientAsync("shop", Roles.Shop);
        var version = await VersionAsync(client, shopToken);
        using var first = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, Contacts(version, telegram: "first_admin"));
        first.EnsureSuccessStatusCode();

        // A second shop admin saves the page they opened before the first save.
        using var second = await SendAsync(client, shopToken, HttpMethod.Put, ContactsPath, Contacts(version, telegram: "second_admin"));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadErrorCodeAsync()).ShouldBe("Shop.ChangedConcurrently");
        using var read = await SendAsync(client, shopToken, HttpMethod.Get, ContactsPath);
        (await read.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Telegram.ShouldBe("first_admin");
    }

    [Theory]
    [InlineData("mobile_phone", "09121234567")]
    [InlineData("whats_app_phone", "+14155550100")]
    [InlineData("landline_phone", "071-32345678")]
    [InlineData("telegram", "@pasargad")]
    [InlineData("instagram", "a b")]
    public async Task ShopSettingsTable_AValueOutOfShape_IsRefusedByTheDatabase(string column, string value)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The column name comes from the test's own data, never from input.
#pragma warning disable EF1002
        var update = () => db.Database.ExecuteSqlRawAsync(
            $"UPDATE shop_settings SET {column} = {{0}}",
            [value],
            TestContext.Current.CancellationToken);
#pragma warning restore EF1002

        (await update.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public void Migration_SeedsOneShopSettingsRowWithNoContactWay()
    {
        Fixture.ShopSettingsAfterMigration.ShouldBe([(ShopSettings.TheId, 0)]);
    }

    [Fact]
    public async Task ShopSettingsTable_ASecondRow_IsRefusedByTheDatabase()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var insert = () => db.Database.ExecuteSqlAsync(
            $"INSERT INTO shop_settings (id, created_at) VALUES ({Guid.CreateVersion7()}, now())",
            TestContext.Current.CancellationToken);

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        (await db.ShopSettings.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await db.ShopSettings.SingleAsync(TestContext.Current.CancellationToken)).Id.ShouldBe(ShopSettings.TheId);
    }

    [Fact]
    public async Task UserRolesTable_ASecondRoleForOneAccount_IsRefusedByTheDatabase()
    {
        await ClientAsync("owner", Roles.Owner);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleId = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO roles (id, name, normalized_name) VALUES ({roleId}, 'Second', 'SECOND')",
            TestContext.Current.CancellationToken);

        var insert = () => db.Database.ExecuteSqlAsync(
            $"INSERT INTO user_roles (user_id, role_id) SELECT id, {roleId} FROM users WHERE user_name = 'owner'",
            TestContext.Current.CancellationToken);

        var refused = await insert.ShouldThrowAsync<PostgresException>();
        refused.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        refused.ConstraintName.ShouldBe("ux_user_roles_user_id");
    }

    /// <summary>
    /// Every endpoint a shop admin must not reach: all but the anonymous ones, the site's (a site
    /// token's scheme, covered by <c>SchemeSeparationTests</c>) and the few that admit the shop admin.
    /// </summary>
    private List<(string Method, string Path)> GymEndpoints() =>
        [.. Fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is not null)
            .Where(endpoint => !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => AdmitsTheShopAdminOrIsSite(data.Policy)))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (method, Fill(endpoint.RoutePattern.RawText!))))];

    private static bool AdmitsTheShopAdminOrIsSite(string? policy) =>
        AdmitsTheShopAdmin(policy) || policy is Policies.SiteAccount or Policies.SiteMember or Policies.SiteTrainer;

    private static bool AdmitsTheShopAdmin(string? policy) =>
        policy is Policies.ShopAdmin or Policies.ShopOrOwner or Policies.OwnerStaffOrShop or Policies.PasswordChangeAllowed;

    /// <summary>An int where the route wants one, a new id everywhere else.</summary>
    private static string Fill(string route) =>
        RouteValue().Replace(route, match => match.Groups["type"].Value == "int" ? "1" : Guid.CreateVersion7().ToString());

    private async Task<(HttpClient Client, string Token)> ClientAsync(string userName, string role)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName, role);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync(userName, TestUsers.Password));
    }

    private static async Task<uint> VersionAsync(HttpClient client, string token)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, ContactsPath);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Version;
    }

    private static object Contacts(uint version, string telegram = "pasargad_gym") =>
        new { mobilePhone = "09121234567", telegram, version };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(:(?<type>[a-z]+))?[^}]*\}")]
    private static partial Regex RouteValue();
}

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Identity;

/// <summary>
/// <see cref="IdentitySeeder.SeedOwnerAsync"/> against the real Postgres schema. Each test
/// builds its own <see cref="IConfiguration"/> with the <c>Seed:*</c> values it needs, rather
/// than resolving one from the app, because the app's own configuration in the Testing
/// environment deliberately carries none — see BUSINESS_RULES.md §1 and the comment on
/// <c>IdentitySeeder.SeedOwnerAsync</c> for why.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class IdentitySeederTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ValidPassword = "Owner1234";

    [Fact]
    public async Task SeedOwnerAsync_WhenCalledTwice_CreatesExactlyOneOwner()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = BuildConfiguration(userName: "owner", password: ValidPassword);

        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);
        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var owners = await userManager.GetUsersInRoleAsync(Roles.Owner);

        owners.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenOwnerAlreadyExists_NeverChangesTheExistingOwnersUserNameOrPassword()
    {
        await using var scope = Fixture.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var firstConfiguration = BuildConfiguration(userName: "owner", password: ValidPassword);
        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, firstConfiguration, TestContext.Current.CancellationToken);

        var originalOwner = (await userManager.GetUsersInRoleAsync(Roles.Owner)).ShouldHaveSingleItem();
        var originalPasswordHash = originalOwner.PasswordHash;

        // A later run with a changed Seed:OwnerUserName must not rename, replace, or touch
        // the password of the Owner that already exists.
        var secondConfiguration = BuildConfiguration(userName: "new-owner", password: "Different123");
        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, secondConfiguration, TestContext.Current.CancellationToken);

        var owners = await userManager.GetUsersInRoleAsync(Roles.Owner);
        var owner = owners.ShouldHaveSingleItem();

        owner.Id.ShouldBe(originalOwner.Id);
        owner.UserName.ShouldBe("owner");
        owner.PasswordHash.ShouldBe(originalPasswordHash);
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenSeeded_CreatesOwnerAndStaffRoles()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = BuildConfiguration(userName: "owner", password: ValidPassword);

        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        (await roleManager.RoleExistsAsync(Roles.Owner)).ShouldBeTrue();
        (await roleManager.RoleExistsAsync(Roles.Staff)).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenSeeded_SetsMustChangePasswordTrue()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = BuildConfiguration(userName: "owner", password: ValidPassword);

        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var owner = await userManager.FindByNameAsync("owner");

        owner.ShouldNotBeNull();
        owner.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenOwnerFullNameIsNotConfigured_DefaultsToManager()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = BuildConfiguration(userName: "owner", password: ValidPassword, fullName: null);

        await IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var owner = await userManager.FindByNameAsync("owner");

        owner.ShouldNotBeNull();
        owner.FullName.ShouldBe("مدیر");
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenSeedPasswordFailsThePolicy_DoesNotCreateAnOwner()
    {
        await using var scope = Fixture.CreateScope();
        // 6 characters: below the configured RequiredLength of 8 (see AddInfrastructure).
        var configuration = BuildConfiguration(userName: "owner", password: "abc123");

        await Should.NotThrowAsync(
            () => IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        userManager.Users.ShouldBeEmpty();
    }

    [Fact]
    public async Task SeedOwnerAsync_WhenSeedConfigurationIsMissing_DoesNothingAndDoesNotThrow()
    {
        await using var scope = Fixture.CreateScope();
        var configuration = new ConfigurationBuilder().Build();

        await Should.NotThrowAsync(
            () => IdentitySeeder.SeedOwnerAsync(scope.ServiceProvider, configuration, TestContext.Current.CancellationToken));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        userManager.Users.ShouldBeEmpty();
    }

    private static IConfiguration BuildConfiguration(string userName, string password, string? fullName = "مدیر تست")
    {
        var values = new Dictionary<string, string?>
        {
            ["Seed:OwnerUserName"] = userName,
            ["Seed:OwnerPassword"] = password,
        };

        if (fullName is not null)
        {
            values["Seed:OwnerFullName"] = fullName;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}

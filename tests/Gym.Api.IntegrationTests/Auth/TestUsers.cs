using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>Creates users through the real <see cref="UserManager{TUser}"/>, so passwords are really hashed.</summary>
internal static class TestUsers
{
    public const string Password = "Staff1234";

    public static async Task<User> CreateAsync(
        DatabaseFixture fixture,
        string userName = "staff",
        string role = Roles.Staff)
    {
        await using var scope = fixture.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            (await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded.ShouldBeTrue();
        }

        var user = new User(userName, "کاربر تست");
        (await userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();

        return user;
    }

    /// <summary>
    /// Deactivates with SQL, because the <c>Deactivate</c> use case belongs to task 1.5. The
    /// login code only reads the column, so how it was set does not matter to these tests.
    /// </summary>
    public static async Task DeactivateAsync(DatabaseFixture fixture, Guid userId)
    {
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE users SET is_active = false WHERE id = {userId}",
            TestContext.Current.CancellationToken);
    }

    public static async Task<int> GetAccessFailedCountAsync(DatabaseFixture fixture, Guid userId)
    {
        await using var scope = fixture.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user.ShouldNotBeNull().AccessFailedCount;
    }
}

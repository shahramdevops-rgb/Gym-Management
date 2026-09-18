using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Seeds the <see cref="Roles.Owner"/> and <see cref="Roles.Staff"/> roles and, from
/// configuration, the one Owner account every gym needs at first boot. See BUSINESS_RULES.md
/// §1 for the exact rules this follows.
/// </summary>
public static partial class IdentitySeeder
{
    /// <summary>
    /// Configuration is passed in explicitly, rather than resolved from <paramref name="services"/>,
    /// so a test can supply its own <c>Seed:*</c> values against a database the app's own
    /// configuration (which has none) already migrated.
    /// </summary>
    public static async Task SeedOwnerAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IdentitySeeder).FullName!);

        var userName = configuration["Seed:OwnerUserName"];
        var password = configuration["Seed:OwnerPassword"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            // No database call on this path: at test-host startup this runs before migrations
            // apply, and the Roles/Users tables do not exist yet.
            LogSeedConfigurationMissing(logger);
            return;
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await EnsureRoleAsync(roleManager, Roles.Owner);
        await EnsureRoleAsync(roleManager, Roles.Staff);

        var userManager = services.GetRequiredService<UserManager<User>>();

        var existingOwners = await userManager.GetUsersInRoleAsync(Roles.Owner);
        if (existingOwners.Count > 0)
        {
            // Idempotent: an Owner already exists, even if Seed:OwnerUserName has since
            // changed. That Owner, including its password, is never touched by seeding.
            return;
        }

        var fullName = configuration["Seed:OwnerFullName"];
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = "مدیر";
        }

        // CreateAsync and AddToRoleAsync each call SaveChanges, so without a transaction a crash
        // between them would leave a user who is not an Owner. The next run would find no Owner,
        // try to create the same user name again, fail on DuplicateUserName, and the gym would
        // never get an Owner. UserManager uses the same scoped AppDbContext, so both saves join
        // this transaction.
        var dbContext = services.GetRequiredService<AppDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var owner = new User(userName, fullName);
        var createResult = await userManager.CreateAsync(owner, password);
        if (!createResult.Succeeded)
        {
            // A misconfigured seed password (fails the policy in AddInfrastructure) must not
            // crash startup; it leaves the gym with no Owner and a warning that says why.
            // Disposing the transaction without committing rolls back anything written so far.
            LogOwnerSeedingFailed(logger, Describe(createResult));
            return;
        }

        // Unlike a bad password, this failing is not a configuration mistake, so it throws.
        ThrowIfFailed(await userManager.AddToRoleAsync(owner, Roles.Owner), "add the Owner to its role");

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)), $"create role {roleName}");
        }
    }

    /// <summary>
    /// Identity reports failures as an <see cref="IdentityResult"/>, not an exception, so an
    /// unchecked result is a silent failure. Anything here besides a bad seed password is
    /// unexpected, and CLAUDE.md reserves exceptions for exactly that.
    /// </summary>
    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Identity seeding could not {action}: {Describe(result)}");
        }
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Seed:OwnerUserName or Seed:OwnerPassword is not configured; Owner seeding was skipped.")]
    private static partial void LogSeedConfigurationMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Owner seeding failed: {Errors}")]
    private static partial void LogOwnerSeedingFailed(ILogger logger, string errors);
}

using Gym.Application.Common;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Interceptors;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

namespace Gym.Infrastructure;

/// <summary>
/// The one entry point Gym.Api uses to wire up this layer. Keeping the registrations here
/// rather than in <c>Program.cs</c> means the composition root names a layer, not a provider.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = BuildConnectionString(configuration);

        // TryAdd, because task 0.4 registers TimeProvider in Program.cs as well. The rule in
        // CLAUDE.md is that nothing calls DateTime.UtcNow, so the clock has to be injectable
        // and the tests can substitute FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        // Stateless and depends only on a singleton, so one instance serves every context.
        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            // PascalCase CLR names to snake_case database names, so C# reads like C# and SQL
            // reads like SQL. It also removes the need to quote identifiers by hand in
            // Postgres, which folds unquoted names to lower case.
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>()));

        // Application asks for the interface; it resolves to the same scoped instance the
        // framework already tracks, so both views share one change tracker and one transaction.
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // AddIdentityCore, not AddIdentity: there is no cookie sign-in yet. Task 1.2 adds JWT
        // bearer auth and can add SignInManager then, if it turns out to need it, instead of
        // this task carrying services nothing here uses.
        services.AddIdentityCore<User>(options =>
            {
                // BUSINESS_RULES.md §1: at least 8 characters, a letter and a digit, no case
                // or symbol requirement — passwords are typed on a Persian keyboard at the
                // front desk. The built-in per-class checks are switched off in favor of
                // LetterAndDigitPasswordValidator, which checks letter/digit without regard
                // to case.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddPasswordValidator<LetterAndDigitPasswordValidator>();

        // Without this, /health would only report that the process is running, and an
        // orchestrator would happily route traffic to an API that cannot reach its database.
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }

    /// <summary>
    /// Recombines the committed half of the connection string with the secret half. The
    /// password is a separate configuration key because a configuration value cannot have
    /// child keys, so it cannot be layered onto <c>ConnectionStrings:Postgres</c> directly.
    /// Locally it comes from user-secrets, in production from an environment variable.
    /// </summary>
    private static string BuildConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var password = configuration["Postgres:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            builder.Password = password;
        }

        // Fail at startup with an actionable message rather than at the first query with a
        // bare authentication error, the same reasoning as ${VAR:?...} in docker-compose.yml.
        if (string.IsNullOrWhiteSpace(builder.Password))
        {
            throw new InvalidOperationException(
                "No database password was configured. Locally, run: dotnet user-secrets set " +
                "\"Postgres:Password\" \"<password from .env>\" --project src/Gym.Api. " +
                "In production, supply Postgres__Password or a full ConnectionStrings__Postgres.");
        }

        return builder.ConnectionString;
    }
}

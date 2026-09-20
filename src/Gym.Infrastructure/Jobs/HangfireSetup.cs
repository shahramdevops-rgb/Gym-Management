using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Wires Hangfire's job queue into the same Postgres database as the app. Called from
/// <c>AddInfrastructure</c>, the same place every other piece of infrastructure is registered.
/// </summary>
public static class HangfireSetup
{
    /// <summary>
    /// A schema of its own, separate from the app's tables in <c>public</c>. That is what keeps
    /// the integration test harness's Respawn reset — scoped to
    /// <c>SchemasToInclude = ["public"]</c> — from ever touching Hangfire's own bookkeeping.
    /// </summary>
    public const string SchemaName = "hangfire";

    public static IServiceCollection AddHangfireJobs(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var storageOptions = new PostgreSqlStorageOptions { SchemaName = SchemaName };

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseConnectionFactory(new NpgsqlConnectionFactory(connectionString, storageOptions)),
                storageOptions));

        // Runs jobs in-process, the same host as the API. No separate worker process for a
        // single-gym deployment this size.
        services.AddHangfireServer();

        return services;
    }
}

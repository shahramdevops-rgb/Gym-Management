using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Health;

/// <summary>
/// The check that names a database older than the code (see <see cref="PendingMigrationsHealthCheck"/>).
/// </summary>
/// <remarks>
/// The healthy direction is already covered by <c>HealthEndpointTests</c>: the whole report is
/// Healthy there, which it cannot be unless this check passes against the migrated fixture
/// database. What needs its own test is the unhealthy direction, and it uses a scratch database
/// created and dropped here rather than the shared one — emptying the real
/// <c>__EFMigrationsHistory</c> would survive Respawn (which ignores that table) and leave the
/// next run's migrations half-applied.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PendingMigrationsHealthCheckTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task CheckHealth_DatabaseMissingEveryMigration_IsUnhealthyAndNamesTheFix()
    {
        var database = $"pending_migrations_probe_{Guid.CreateVersion7():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE \"{database}\"");

        try
        {
            await using var db = ContextFor(database);

            var result = await new PendingMigrationsHealthCheck(db)
                .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Unhealthy);
            // The message has to say what to do: this check exists so nobody has to work the
            // cause out from a 500 on an unrelated screen.
            result.Description.ShouldNotBeNull();
            result.Description.ShouldContain("dotnet ef database update");
            result.Description.ShouldContain("InitialCreate");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        }
    }

    [Fact]
    public async Task CheckHealth_MigratedDatabase_IsHealthy()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await new PendingMigrationsHealthCheck(db)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    private AppDbContext ContextFor(string database)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(Fixture.ConnectionString) { Database = database }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        // CREATE/DROP DATABASE cannot run inside the database being created, so these go to the
        // container's default one.
        var connectionString = new NpgsqlConnectionStringBuilder(Fixture.ConnectionString) { Database = "postgres" }.ToString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

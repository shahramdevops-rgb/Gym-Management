using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Respawn;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Tests of the harness itself. If these break, every test written on top of it is suspect.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DatabaseHarnessTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Fixture_WhenStarted_HasAppliedTheMigrations()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        // The schema comes from the migrations, so a migration that does not apply cleanly
        // fails here rather than in the first feature test that happens to touch a new column.
        applied.ShouldContain(migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RespawnOptions_WhenResetting_DeleteRowsButKeepTheMigrationHistory()
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // A real table with a real row, because Respawn builds its delete plan from the schema
        // it finds — asserting on the options object alone would prove nothing about what it
        // actually does. The plan is built after the probe table exists, which is also why
        // this creates its own Respawner instead of reusing the fixture's.
        await ExecuteAsync(connection, "CREATE TABLE respawn_probe (id integer PRIMARY KEY)");
        await ExecuteAsync(connection, "INSERT INTO respawn_probe (id) VALUES (1)");

        try
        {
            var respawner = await Respawner.CreateAsync(connection, DatabaseFixture.RespawnOptions);

            await respawner.ResetAsync(connection);

            (await CountAsync(connection, "SELECT count(*) FROM respawn_probe"))
                .ShouldBe(0, "a reset must leave the tables empty for the next test.");

            // The failure this guards against: Respawn deletes rows, not tables, so wiping
            // __EFMigrationsHistory would leave a fully migrated database that believes it has
            // never been migrated — and the next run would try to apply InitialCreate again.
            (await CountAsync(connection, "SELECT count(*) FROM \"__EFMigrationsHistory\""))
                .ShouldBeGreaterThan(0, "the migration history must survive a reset.");
        }
        finally
        {
            await ExecuteAsync(connection, "DROP TABLE respawn_probe");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);

        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}

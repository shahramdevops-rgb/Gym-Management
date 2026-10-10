using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Performance;

/// <summary>
/// The connection and index settings from task 11.3 (docs/performance-review.md). None changes a
/// result, only how long one takes or what the log says, so no other test would notice them gone.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DatabaseTuningTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task AppConnection_Opened_HasJitOff()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jit = await db.Database
            .SqlQuery<string>($"SELECT current_setting('jit') AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        jit.ShouldBe("off");
    }

    [Fact]
    public void AppConnection_Built_DoesNotTryKerberos()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());

        connection.GssEncryptionMode.ShouldBe(GssEncryptionMode.Disable);
    }

    [Fact]
    public async Task PaymentsTable_AfterMigrations_HasIndexOnCafeOrderId()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var definitions = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'payments'")
            .ToListAsync(TestContext.Current.CancellationToken);

        definitions.ShouldContain(definition => definition.EndsWith("USING btree (cafe_order_id)", StringComparison.Ordinal));
    }
}

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The migration <c>AddSmsDeliveryCancelled</c> carries a blocked message forward instead of refusing
/// it (CLAUDE.md): Kavenegar gave its cost back, so its cost becomes 0, as the new check requires
/// (BUSINESS_RULES.md §10 <i>Sending</i>). Every other message keeps its cost.
/// </summary>
/// <remarks>
/// Like <see cref="SendSmsAsTextMigrationTests"/>, it runs in a database of its own, migrated only up
/// to the migration before, with the foreign keys switched off while the old rows go in.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AddSmsDeliveryCancelledMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261008200203_SendSmsAsText";

    [Fact]
    public async Task Migrate_BlockedMessageWithACost_CostsNothingAndTheOthersKeepTheirCost()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        await ExecuteAsync(
            connectionString,
            """
            SET session_replication_role = replica;
            INSERT INTO notifications (id, created_at, kind, recipient, member_id, subscription_id, payable_id, jalali_year,
                text, status, attempts, provider_message_id, sent_at, cost_rial, delivery)
            VALUES
                (gen_random_uuid(), now(), 'Birthday', '+989121234567', gen_random_uuid(), NULL, NULL, 1405,
                    'تولدتان مبارک', 'Sent', 1, 1, now(), 1350, 'BlockedByReceiver'),
                (gen_random_uuid(), now(), 'Birthday', '+989121234568', gen_random_uuid(), NULL, NULL, 1405,
                    'تولدتان مبارک', 'Sent', 1, 2, now(), 1350, 'Delivered'),
                (gen_random_uuid(), now(), 'Birthday', '+989121234569', gen_random_uuid(), NULL, NULL, 1405,
                    'تولدتان مبارک', 'Sent', 1, 3, now(), 1350, 'NotDelivered');
            SET session_replication_role = DEFAULT;
            """);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var costs = await db.Notifications.AsNoTracking()
            .ToDictionaryAsync(notification => notification.Delivery!.Value, notification => notification.CostRial, TestContext.Current.CancellationToken);
        costs.Count.ShouldBe(3);
        costs[SmsDelivery.BlockedByReceiver].ShouldBe(0m);
        costs[SmsDelivery.Delivered].ShouldBe(1_350m);
        costs[SmsDelivery.NotDelivered].ShouldBe(1_350m);
    }

    private static AppDbContext Context(string connectionString) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>A new, empty database next to the shared one; the container is thrown away after the run.</summary>
    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"gym_migration_{Guid.NewGuid():N}";
        await ExecuteAsync(fixture.ConnectionString, $"CREATE DATABASE {name}");

        return new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

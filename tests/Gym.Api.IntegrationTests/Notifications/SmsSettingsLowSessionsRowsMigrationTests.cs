using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The migration <c>SmsSettingsLowSessionsRowsAndReportTime</c> (task 10.17) carries the Owner's
/// settings forward instead of losing or refusing them (CLAUDE.md): the one «جلسات رو به اتمام» number
/// becomes all three rows', capped at the new 4, the rows are divided at 8 and 15 sessions, the kind
/// stays on, and the daily report keeps the 22:45 it had.
/// </summary>
/// <remarks>
/// Like <see cref="AddSmsDeliveryCancelledMigrationTests"/>, it runs in a database of its own,
/// migrated only up to the migration before.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsSettingsLowSessionsRowsMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261022223125_AddSmsAutoResends";

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(10, 4)]
    public async Task Migrate_LowSessionsOn_EveryRowGetsTheOldNumberAndItStaysOn(int before, int after)
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        await ExecuteAsync(
            connectionString,
            $"""
            UPDATE sms_settings
            SET low_sessions_enabled = true, low_sessions_threshold = {before}, low_sessions_send_time = '18:30',
                daily_report_enabled = true, daily_report_phone = '+989121234567';
            """);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var settings = await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        settings.LowSessions.ShouldBe(new LowSessionsSettings(true, 8, 15, after, after, after, new TimeOnly(18, 30)));
        settings.DailyReportEnabled.ShouldBeTrue();
        settings.DailyReportSendTime.ShouldBe(new TimeOnly(22, 45));
    }

    [Fact]
    public async Task Migrate_LowSessionsNeverFilled_RowsStayEmptyWithThePlanSizesReady()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var settings = await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        settings.LowSessions.ShouldBe(new LowSessionsSettings(false, 8, 15, null, null, null, null));
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

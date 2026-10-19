using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// The migration <c>AddBirthdayVisit</c> (task 10.8) carries the Owner's settings forward (CLAUDE.md:
/// a migration never refuses existing rows): the birthday SMS is no longer sent on the day itself, so
/// a 0 saved before becomes 1 and the kind stays on.
/// </summary>
/// <remarks>
/// A database of its own in the same container, migrated only up to the migration before, the same
/// way as <c>SendSmsAsTextMigrationTests</c>.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AddBirthdayVisitMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261019093518_AddSmsDailyReport";

    [Fact]
    public async Task Migrate_BirthdaySentOnTheDayItself_BecomesOneDayBeforeAndStaysOn()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);
        await ExecuteAsync(
            connectionString,
            "UPDATE sms_settings SET birthday_enabled = true, birthday_days_before = 0, birthday_send_time = '10:00'");

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var birthday = (await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken))
            .For(NotificationKind.Birthday);
        birthday.ShouldBe(new SmsKindSettings(Enabled: true, 1, new TimeOnly(10, 0)));
    }

    [Fact]
    public async Task Migrate_BirthdayDaysAlreadyAllowed_AreKept()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);
        await ExecuteAsync(
            connectionString,
            "UPDATE sms_settings SET birthday_enabled = true, birthday_days_before = 5, birthday_send_time = '09:15'");

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var birthday = (await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken))
            .For(NotificationKind.Birthday);
        birthday.ShouldBe(new SmsKindSettings(Enabled: true, 5, new TimeOnly(9, 15)));
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

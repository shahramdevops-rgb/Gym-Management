using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// The migration <c>SubscriptionsAtMostThirtySessions</c> (task 6.5.38) refuses none of the rows the
/// server holds (CLAUDE.md): the plan sold with 123 sessions by mistake and cancelled stays as it
/// was, and a «جلسات رو به اتمام» row size above the new ceiling comes down to it.
/// </summary>
/// <remarks>
/// Like <see cref="Notifications.SmsSettingsLowSessionsRowsMigrationTests"/>, it runs in a database
/// of its own, migrated only up to the migration before.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionsAtMostThirtySessionsMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261022232647_SmsSettingsLowSessionsRowsAndReportTime";

    [Fact]
    public async Task Migrate_CancelledPlanOf123Sessions_IsKeptAsItWas()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);
        var member = TestMembers.Seed("رضا احمدی", "+989121234567");
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            connectionString,
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days,
                                       cancelled_at, cancellation_reason, is_single_session, created_at)
            VALUES ('{id}', '{member.Id}', 0, 70, 123, '2026-10-20', '2026-12-28', 0, 0,
                    now(), 'اشتباه تایپی', false, now());
            """);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var plan = await db.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        plan.TotalSessions.ShouldBe(123);
        plan.CancelledAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(8, 15, 8, 15)] // the server's, 1405/08/03
    [InlineData(40, 100, 29, 30)]
    [InlineData(20, 50, 20, 30)]
    public async Task Migrate_LowSessionsRowSizes_ComeDownToTheNewCeiling(
        int smallBefore, int mediumBefore, int smallAfter, int mediumAfter)
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        await ExecuteAsync(
            connectionString,
            $"UPDATE sms_settings SET low_sessions_small_plan_max = {smallBefore}, low_sessions_medium_plan_max = {mediumBefore};");

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var settings = await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        settings.LowSessions.SmallPlanMax.ShouldBe(smallAfter);
        settings.LowSessions.MediumPlanMax.ShouldBe(mediumAfter);
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

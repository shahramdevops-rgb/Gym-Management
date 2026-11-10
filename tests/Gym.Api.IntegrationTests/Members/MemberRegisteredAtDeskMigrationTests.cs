using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// The migration <c>AddMemberRegisteredAtDesk</c> (task 16.1) on the rows the server holds
/// (CLAUDE.md: refuse none, carry them forward): every member the desk registered stays so, and only
/// the members the site's sign-up made are marked.
/// </summary>
/// <remarks>
/// Like <see cref="Subscriptions.SubscriptionsAtMostThirtySessionsMigrationTests"/>: a database of
/// its own, migrated up to the migration before, rows written in SQL with that day's columns, then
/// migrated to the end.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MemberRegisteredAtDeskMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261104120431_LimitSessionHours";

    [Fact]
    public async Task Migrate_EachKindOfExistingMember_MarksOnlyTheSiteSignUps()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        var staffUser = Guid.CreateVersion7();
        await ExecuteAsync(
            connectionString,
            $"""
            {Member("+989121110001", staffUser)}   -- registered at the desk
            {Member("+989121110002", staffUser)}   -- registered at the desk, signed in on the site later
            {Account("+989121110002")}
            {Member("+989121110003", null)}        -- signed up on the site
            {Account("+989121110003")}
            {Member("+989121110004", null)}        -- signed up on the site, bought a plan since
            {Account("+989121110004")}
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions, start_date, end_date,
                                       used_sessions, total_frozen_days, is_single_session, created_at)
            SELECT '{Guid.CreateVersion7()}', id, 900000, 30, 10, '2026-11-01', '2026-11-30', 0, 0, false, now()
            FROM members WHERE phone_number = '+989121110004';
            {Member("+989121110005", null)}        -- made by nobody, no site account (a seed)
            """);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var rows = await db.Members.AsNoTracking()
            .ToDictionaryAsync(member => member.PhoneNumber, member => member.RegisteredAtDesk, TestContext.Current.CancellationToken);
        rows["+989121110001"].ShouldBeTrue();
        rows["+989121110002"].ShouldBeTrue("a desk member who later signed in on the site was registered at the desk.");
        rows["+989121110003"].ShouldBeFalse("a site sign-up.");
        rows["+989121110004"].ShouldBeFalse("a site sign-up; the plan alone keeps them a member (SiteUserRule).");
        rows["+989121110005"].ShouldBeTrue("only a member with a site account can have signed up on the site.");
    }

    [Fact]
    public async Task Migrate_TheDefault_IsDroppedAfterTheExistingRows()
    {
        // The application always writes the value; a row written without one is a bug, refused.
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connectionString, Member("+989121110009", null)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
    }

    private static string Member(string phone, Guid? createdBy) =>
        $"""
        INSERT INTO members (id, full_name, normalized_full_name, phone_number, birth_date, is_active, created_at, created_by)
        VALUES ('{Guid.CreateVersion7()}', 'x', 'x', '{phone}', '1990-06-15', true, now(), {(createdBy is { } id ? $"'{id}'" : "NULL")});
        """;

    private static string Account(string phone) =>
        $"""
        INSERT INTO site_accounts (id, member_id, last_signed_in_at, created_at)
        SELECT '{Guid.CreateVersion7()}', id, now(), now() FROM members WHERE phone_number = '{phone}';
        """;

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

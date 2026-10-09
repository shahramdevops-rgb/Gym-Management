using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The migration <c>SendSmsAsText</c> carries every message written as a template forward
/// (CLAUDE.md: a migration never refuses existing rows): its text is built from the kind and the
/// values it kept, the same text <see cref="SmsText"/> writes today. And a kind the Owner had on
/// stays on.
/// </summary>
/// <remarks>
/// It runs in a database of its own in the same container, migrated only up to the migration
/// before, so the shared test database is never taken back. The foreign keys are switched off while
/// the old rows go in (<c>session_replication_role</c>): the test is about the text, and a member,
/// a plan and a cheque for each row would only hide that.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SendSmsAsTextMigrationTests(DatabaseFixture fixture)
{
    private const string MigrationBefore = "20261007000342_CardioOnlyWithoutPlan";
    private const string Name = "سارا محمدی";

    private static readonly DateOnly Day = new(2026, 10, 12);

    [Fact]
    public async Task Migrate_MessagesWrittenAsTemplates_KeepTheirTextAndKindsStayOn()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = Context(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, cancellationToken: TestContext.Current.CancellationToken);

        var date = SmsText.Date(Day);
        await ExecuteAsync(
            connectionString,
            $"""
            SET session_replication_role = replica;
            INSERT INTO notifications (id, created_at, kind, recipient, member_id, subscription_id, payable_id, jalali_year,
                template_name, token, token2, token3, token10, token20, status, attempts, provider_message_id, sent_at, cost_rial)
            VALUES
                (gen_random_uuid(), now(), 'SubscriptionExpiring', '+989121234567', gen_random_uuid(), gen_random_uuid(), NULL, NULL,
                    'gymExpiring', '{date}', NULL, NULL, '{Name}', NULL, 'Sent', 1, 1, now(), 1350),
                (gen_random_uuid(), now(), 'LowSessions', '+989121234567', gen_random_uuid(), gen_random_uuid(), NULL, NULL,
                    'gymLowSessions', '۲', NULL, NULL, '{Name}', NULL, 'Failed', 3, NULL, NULL, NULL),
                (gen_random_uuid(), now(), 'Birthday', '+989121234567', gen_random_uuid(), NULL, NULL, 1405,
                    'gymBirthday', '{date}', NULL, NULL, '{Name}', NULL, 'Unknown', 1, NULL, NULL, NULL),
                (gen_random_uuid(), now(), 'Birthday', '+989121234568', gen_random_uuid(), NULL, NULL, 1405,
                    'gymBirthdayEarly', '{date}', NULL, NULL, '{Name}', NULL, 'Pending', 0, NULL, NULL, NULL),
                (gen_random_uuid(), now(), 'PayableDue', '+989351112233', NULL, NULL, gen_random_uuid(), NULL,
                    'gymPayableDue', 'چک', '۱۲٬۵۰۰٬۰۰۰', '{date}', NULL, 'فروشگاه تجهیزات ورزشی', 'Sent', 1, 2, now(), 1350);
            SET session_replication_role = DEFAULT;
            UPDATE sms_settings SET birthday_enabled = true, birthday_days_before = 0, birthday_send_time = '10:00',
                birthday_template_name = 'gymBirthday';
            """);

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var texts = await db.Notifications.AsNoTracking()
            .Select(notification => new { notification.Kind, notification.Recipient, notification.Status, notification.Text })
            .ToListAsync(TestContext.Current.CancellationToken);
        texts.Count.ShouldBe(5);
        texts.Single(row => row.Kind == NotificationKind.SubscriptionExpiring).Text.ShouldBe(SmsText.ForRunningOut(Name, Day));
        texts.Single(row => row.Kind == NotificationKind.LowSessions).Text.ShouldBe(SmsText.ForFewSessionsLeft(Name, 2));
        texts.Single(row => row.Recipient == "+989121234567" && row.Kind == NotificationKind.Birthday).Text
            .ShouldBe(SmsText.ForBirthday(Name, Day, today: Day));
        texts.Single(row => row.Recipient == "+989121234568").Text
            .ShouldBe(SmsText.ForBirthday(Name, Day, today: Day.AddDays(-3)));
        texts.Single(row => row.Kind == NotificationKind.PayableDue).Text
            .ShouldBe(SmsText.ForPayableDue(PayableKind.Cheque, 12_500_000m, Day, "فروشگاه تجهیزات ورزشی"));
        texts.Single(row => row.Kind == NotificationKind.LowSessions).Status.ShouldBe(NotificationStatus.Failed);

        var birthday = (await db.SmsSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken))
            .For(NotificationKind.Birthday);
        birthday.ShouldBe(new SmsKindSettings(Enabled: true, 0, new TimeOnly(10, 0)));
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

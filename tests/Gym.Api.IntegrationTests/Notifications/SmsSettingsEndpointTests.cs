using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Domain.Audit;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The SMS settings page: <c>GET</c> and <c>PUT /api/sms/settings</c> (BUSINESS_RULES.md §10
/// <i>SMS settings</i>, task 10.2). Owner only; starts empty and off.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsSettingsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/settings";

    private static readonly object Off = new { enabled = false, threshold = (int?)null, sendTime = (string?)null };

    private static readonly object ReportOff = new { enabled = false, phone = (string?)null };

    private static readonly object ReceiptOff = new { enabled = false };
    private static readonly object WelcomeOff = new { enabled = false };

    private static readonly object BroadcastOff = new { enabled = false };

    private static readonly object DebtReminderOff = new
    {
        enabled = false,
        daysAfter = (int?)null,
        sendTime = (string?)null,
        minAmount = (decimal?)null,
    };

    private static readonly object AbsenceOff = Off;

    // ---- The seeded row ----

    [Fact]
    public void Migration_SeedsOneRowWithEverythingOffAndEmpty()
    {
        // Read right after migrating, before any reset: the migration's own row, not the fixture's copy.
        var row = Fixture.SmsSettingsAfterMigration.ShouldHaveSingleItem();

        row.Id.ShouldBe(SmsSettings.TheId);
        row.AnyOn.ShouldBeFalse();
        row.FilledFields.ShouldBe(0);
    }

    [Fact]
    public async Task Database_SecondRow_IsRefused()
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            $"""
            INSERT INTO sms_settings (id, created_at, enabled, subscription_expiring_enabled, low_sessions_enabled,
                birthday_enabled, payable_due_enabled)
            VALUES ('{Guid.CreateVersion7()}', now(), false, false, false, false, false)
            """));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_sms_settings_single_row");
    }

    [Theory]
    [InlineData("subscription_expiring_days_before = 0", "ck_sms_settings_subscription_expiring_days")]
    [InlineData("subscription_expiring_days_before = 31", "ck_sms_settings_subscription_expiring_days")]
    [InlineData("low_sessions_threshold = 0", "ck_sms_settings_low_sessions_threshold")]
    [InlineData("low_sessions_threshold = 11", "ck_sms_settings_low_sessions_threshold")]
    [InlineData("birthday_days_before = 0", "ck_sms_settings_birthday_days")]
    [InlineData("birthday_days_before = 8", "ck_sms_settings_birthday_days")]
    [InlineData("payable_due_days_before = 31", "ck_sms_settings_payable_due_days")]
    [InlineData("birthday_send_time = '07:45'", "ck_sms_settings_send_times")]
    [InlineData("birthday_send_time = '22:15'", "ck_sms_settings_send_times")]
    [InlineData("low_sessions_send_time = '10:10'", "ck_sms_settings_send_times")]
    [InlineData("payable_due_send_time = '10:00:30'", "ck_sms_settings_send_times")]
    [InlineData("owner_phone = '09121234567'", "ck_sms_settings_owner_phone")]
    [InlineData("owner_phone = '+982112345678'", "ck_sms_settings_owner_phone")]
    [InlineData("subscription_expiring_enabled = true", "ck_sms_settings_subscription_expiring_on_means_filled")]
    [InlineData("low_sessions_enabled = true", "ck_sms_settings_low_sessions_on_means_filled")]
    [InlineData("birthday_enabled = true", "ck_sms_settings_birthday_on_means_filled")]
    [InlineData(
        "payable_due_enabled = true, payable_due_days_before = 3, payable_due_send_time = '10:00'",
        "ck_sms_settings_payable_due_on_means_filled")]
    [InlineData("daily_report_phone = '09121234567'", "ck_sms_settings_daily_report_phone")]
    [InlineData("daily_report_enabled = true", "ck_sms_settings_daily_report_on_means_filled")]
    [InlineData("debt_reminder_days_after = 0", "ck_sms_settings_debt_reminder_days")]
    [InlineData("debt_reminder_days_after = 91", "ck_sms_settings_debt_reminder_days")]
    [InlineData("debt_reminder_min_amount = 999.99", "ck_sms_settings_debt_reminder_min_amount")]
    [InlineData("debt_reminder_send_time = '22:15'", "ck_sms_settings_send_times")]
    [InlineData("absence_days_away = 6", "ck_sms_settings_absence_days")]
    [InlineData("absence_days_away = 61", "ck_sms_settings_absence_days")]
    [InlineData("absence_send_time = '07:45'", "ck_sms_settings_send_times")]
    [InlineData("absence_enabled = true, absence_days_away = 14", "ck_sms_settings_absence_on_means_filled")]
    [InlineData(
        "debt_reminder_enabled = true, debt_reminder_days_after = 30, debt_reminder_send_time = '11:00'",
        "ck_sms_settings_debt_reminder_on_means_filled")]
    public async Task Database_RuleBroken_IsRefused(string assignments, string constraint)
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync($"UPDATE sms_settings SET {assignments}"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task Database_DebtReminderOnAndFilled_IsAccepted() =>
        await ExecuteAsync(
            """
            UPDATE sms_settings SET debt_reminder_enabled = true, debt_reminder_days_after = 90,
                debt_reminder_send_time = '08:00', debt_reminder_min_amount = 1000
            """);

    [Fact]
    public async Task Database_KindOnAndFilled_IsAccepted() =>
        await ExecuteAsync(
            """
            UPDATE sms_settings SET birthday_enabled = true, birthday_days_before = 1,
                birthday_send_time = '22:00'
            """);

    // ---- Get ----

    [Fact]
    public async Task Get_BeforeTheOwnerFillsIt_ReturnsEverythingOffAndEmpty()
    {
        var (client, token, _) = await OwnerClientAsync();

        var settings = await GetOkAsync(client, token);

        settings.Enabled.ShouldBeFalse();
        settings.SubscriptionExpiring.ShouldBe(SmsKindSettings.Off);
        settings.LowSessions.ShouldBe(SmsKindSettings.Off);
        settings.Birthday.ShouldBe(SmsKindSettings.Off);
        settings.PayableDue.ShouldBe(SmsKindSettings.Off);
        settings.OwnerPhone.ShouldBeNull();
        settings.DailyReport.ShouldBe(new DailyReportSettingsResponse(false, null, new TimeOnly(22, 45)));
        settings.SubscriptionReceipt.ShouldBe(new SubscriptionReceiptSettingsResponse(false, new TimeOnly(10, 0)));
        settings.Welcome.ShouldBe(new WelcomeSettingsResponse(false, new TimeOnly(10, 0)));
        settings.DebtReminder.ShouldBe(new DebtReminderSettings(false, null, null, null));
        settings.Absence.ShouldBe(SmsKindSettings.Off);
        settings.Broadcast.ShouldBe(new BroadcastSettings(false));
    }

    [Fact]
    public async Task Get_AsStaff_Returns403()
    {
        var (client, token) = await StaffClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_AsOwner_SavesEveryKindAndTheNumber()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, FullBody(before.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync(response);
        updated.Version.ShouldNotBe(before.Version);

        var reread = await GetOkAsync(client, token);
        reread.Enabled.ShouldBeTrue();
        reread.SubscriptionExpiring.ShouldBe(new SmsKindSettings(true, 7, new TimeOnly(9, 0)));
        reread.LowSessions.ShouldBe(new SmsKindSettings(true, 2, new TimeOnly(9, 15)));
        reread.Birthday.ShouldBe(new SmsKindSettings(true, 1, new TimeOnly(10, 30)));
        reread.PayableDue.ShouldBe(new SmsKindSettings(true, 3, new TimeOnly(22, 0)));
        reread.OwnerPhone.ShouldBe("+989121234567");
        reread.DailyReport.ShouldBe(new DailyReportSettingsResponse(true, "+989351234567", new TimeOnly(22, 45)));
        reread.SubscriptionReceipt.ShouldBe(new SubscriptionReceiptSettingsResponse(true, new TimeOnly(10, 0)));
        reread.Welcome.ShouldBe(new WelcomeSettingsResponse(true, new TimeOnly(10, 0)));
        reread.Absence.ShouldBe(new SmsKindSettings(true, 14, new TimeOnly(11, 30)));
    }

    // ---- The subscription receipt (task 10.9) ----

    [Fact]
    public async Task Update_SubscriptionReceiptOn_IsSavedWithNothingElseToFill()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version, enabled: true, subscriptionReceipt: new { enabled = true }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(client, token)).SubscriptionReceipt.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_SubscriptionReceiptLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            birthday = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            dailyReport = ReportOff,
            welcome = WelcomeOff,
            broadcast = BroadcastOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "subscriptionReceipt", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_SubscriptionReceiptTurnedOn_IsInTheAuditLog()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        (await UpdateAsync(client, token, Body(read.Version, subscriptionReceipt: new { enabled = true })))
            .EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(SmsSettings) && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.OccurredAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        Value(update.OldValues, nameof(SmsSettings.SubscriptionReceiptEnabled)).GetBoolean().ShouldBeFalse();
        Value(update.NewValues, nameof(SmsSettings.SubscriptionReceiptEnabled)).GetBoolean().ShouldBeTrue();
    }

    // ---- The welcome (task 10.10) ----

    // ---- The debt reminder (task 10.11) ----

    [Fact]
    public async Task Update_DebtReminderOnAndFilled_IsSavedAndGetsItsRun()
    {
        var (client, token, _) = await OwnerClientAsync();
        var schedule = RecordingSchedule();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version,
            enabled: true,
            debtReminder: new { enabled = true, daysAfter = 30, sendTime = "11:00:00", minAmount = 200_000.5m }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(client, token)).DebtReminder.ShouldBe(
            new DebtReminderSettings(true, 30, new TimeOnly(11, 0), 200_000.5m));
        schedule.Applied.ShouldNotBeEmpty();
        schedule.Applied[^1].For(NotificationKind.DebtReminder).SendTime.ShouldBe(new TimeOnly(11, 0));
    }

    [Fact]
    public async Task Update_DebtReminderOnWithoutItsMinimumAmount_Returns400SettingsIncompleteUnderTheSwitch()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            read.Version,
            debtReminder: new { enabled = true, daysAfter = 30, sendTime = "11:00:00", minAmount = (decimal?)null }));

        await ShouldHaveFieldErrorAsync(response, "debtReminder.enabled", "Sms.SettingsIncomplete");
    }

    [Theory]
    [InlineData(0, null, null, "debtReminder.daysAfter", "Sms.DebtReminderDaysOutOfRange")]
    [InlineData(91, null, null, "debtReminder.daysAfter", "Sms.DebtReminderDaysOutOfRange")]
    [InlineData(null, "22:15:00", null, "debtReminder.sendTime", "Sms.SendTimeOutOfRange")]
    [InlineData(null, null, "999.99", "debtReminder.minAmount", "Sms.DebtReminderMinAmountOutOfRange")]
    [InlineData(null, null, "1000.001", "debtReminder.minAmount", "Sms.DebtReminderMinAmountOutOfRange")]
    public async Task Update_DebtReminderFieldOutOfItsRange_Returns400WithTheFieldsCode(
        int? daysAfter, string? sendTime, string? minAmount, string field, string code)
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);
        var amount = minAmount is null ? (decimal?)null : decimal.Parse(minAmount, CultureInfo.InvariantCulture);

        using var response = await UpdateAsync(client, token, Body(
            read.Version, debtReminder: new { enabled = false, daysAfter, sendTime, minAmount = amount }));

        await ShouldHaveFieldErrorAsync(response, field, code);
    }

    [Fact]
    public async Task Update_DebtReminderLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            birthday = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            dailyReport = ReportOff,
            subscriptionReceipt = ReceiptOff,
            welcome = WelcomeOff,
            broadcast = BroadcastOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "debtReminder", "Sms.SettingsIncomplete");
    }

    // ---- The absence SMS (task 10.12) ----

    [Fact]
    public async Task Update_AbsenceOnAndFilled_IsSavedAndGetsItsRun()
    {
        var (client, token, _) = await OwnerClientAsync();
        var schedule = RecordingSchedule();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version, enabled: true, absence: Kind(true, 14, "11:30:00")));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(client, token)).Absence.ShouldBe(new SmsKindSettings(true, 14, new TimeOnly(11, 30)));
        schedule.Applied[^1].For(NotificationKind.Absence).SendTime.ShouldBe(new TimeOnly(11, 30));
    }

    [Theory]
    [InlineData(6, null, "absence.threshold", "Sms.AbsenceDaysOutOfRange")]
    [InlineData(61, null, "absence.threshold", "Sms.AbsenceDaysOutOfRange")]
    [InlineData(null, "11:10:00", "absence.sendTime", "Sms.SendTimeOutOfRange")]
    public async Task Update_AbsenceFieldOutOfItsRange_Returns400WithTheFieldsCode(
        int? days, string? sendTime, string field, string code)
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(read.Version, absence: Kind(false, days, sendTime)));

        await ShouldHaveFieldErrorAsync(response, field, code);
    }

    [Fact]
    public async Task Update_AbsenceOnWithoutItsTime_Returns400SettingsIncompleteUnderTheSwitch()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(read.Version, absence: Kind(true, 14, null)));

        await ShouldHaveFieldErrorAsync(response, "absence.enabled", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_WelcomeOn_IsSavedWithNothingElseToFill()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version, enabled: true, welcome: new { enabled = true }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(client, token)).Welcome.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WelcomeLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            birthday = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            dailyReport = ReportOff,
            subscriptionReceipt = ReceiptOff,
            broadcast = BroadcastOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "welcome", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_WelcomeTurnedOn_IsInTheAuditLog()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        (await UpdateAsync(client, token, Body(read.Version, welcome: new { enabled = true })))
            .EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(SmsSettings) && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.OccurredAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        Value(update.OldValues, nameof(SmsSettings.WelcomeEnabled)).GetBoolean().ShouldBeFalse();
        Value(update.NewValues, nameof(SmsSettings.WelcomeEnabled)).GetBoolean().ShouldBeTrue();
    }

    // ---- The broadcast (task 10.13) ----

    [Fact]
    public async Task Update_BroadcastOn_IsSavedWithNothingElseToFill()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version, enabled: true, broadcast: new { enabled = true }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetOkAsync(client, token)).Broadcast.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_BroadcastLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            birthday = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            dailyReport = ReportOff,
            subscriptionReceipt = ReceiptOff,
            welcome = WelcomeOff,
            debtReminder = DebtReminderOff,
            absence = AbsenceOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "broadcast", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_BroadcastTurnedOn_IsInTheAuditLog()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        (await UpdateAsync(client, token, Body(read.Version, broadcast: new { enabled = true })))
            .EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(SmsSettings) && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.OccurredAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        Value(update.OldValues, nameof(SmsSettings.BroadcastEnabled)).GetBoolean().ShouldBeFalse();
        Value(update.NewValues, nameof(SmsSettings.BroadcastEnabled)).GetBoolean().ShouldBeTrue();
    }

    // ---- The daily report (task 10.7) ----

    [Fact]
    public async Task Update_DailyReportNumberInPersianDigits_IsKeptAsE164ApartFromTheOwnersNumber()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(
            before.Version,
            ownerPhone: "09121234567",
            dailyReport: new { enabled = true, phone = "۰۹۳۵ ۱۲۳ ۴۵۶۷" }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await GetOkAsync(client, token);
        saved.DailyReport.Phone.ShouldBe("+989351234567");
        saved.OwnerPhone.ShouldBe("+989121234567");
    }

    [Fact]
    public async Task Update_DailyReportOnWithoutItsNumber_Returns400SettingsIncompleteUnderTheSwitch()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        // The Owner's number for the cheques does not stand in for the report's.
        using var response = await UpdateAsync(client, token, Body(
            read.Version, ownerPhone: "09121234567", dailyReport: new { enabled = true, phone = (string?)null }));

        await ShouldHaveFieldErrorAsync(response, "dailyReport.enabled", "Sms.SettingsIncomplete");
    }

    [Theory]
    [InlineData("0912")]
    [InlineData("02112345678")]
    public async Task Update_DailyReportNumberNotAnIranianMobile_Returns400WithItsOwnCode(string phone)
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(before.Version, dailyReport: new { enabled = false, phone }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Sms.DailyReportPhoneInvalid");
        (await GetOkAsync(client, token)).DailyReport.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task Update_DailyReportLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            birthday = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            subscriptionReceipt = ReceiptOff,
            welcome = WelcomeOff,
            broadcast = BroadcastOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "dailyReport", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_OwnersNumberInPersianDigits_IsKeptAsE164()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(before.Version, ownerPhone: "۰۹۱۲ ۱۲۳ ۴۵۶۷"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).OwnerPhone.ShouldBe("+989121234567");
    }

    [Fact]
    public async Task Update_EmptyNumberWithChequesOff_ClearsIt()
    {
        var (client, token, _) = await OwnerClientAsync();
        var first = await ReadAsync(await UpdateAsync(client, token, Body((await GetOkAsync(client, token)).Version, ownerPhone: "09121234567")));

        using var response = await UpdateAsync(client, token, Body(first.Version, ownerPhone: ""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).OwnerPhone.ShouldBeNull();
    }

    [Theory]
    [InlineData("0912", "Members.PhoneInvalid")]
    [InlineData("02112345678", "Members.PhoneNotMobile")]
    public async Task Update_OwnersNumberNotAnIranianMobile_Returns400WithThePhoneCode(string phone, string code)
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(before.Version, ownerPhone: phone));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(code);
        (await GetOkAsync(client, token)).OwnerPhone.ShouldBeNull();
    }

    [Fact]
    public async Task Update_AsOwner_WritesTheOldAndNewValuesToTheAuditLog()
    {
        var (client, token, ownerId) = await OwnerClientAsync();
        var first = await ReadAsync(await UpdateAsync(client, token, FullBody((await GetOkAsync(client, token)).Version)));

        (await UpdateAsync(client, token, Body(first.Version, birthday: Kind(false, 3, "10:30:00"))))
            .EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(SmsSettings) && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.OccurredAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        update.UserId.ShouldBe(ownerId);
        Value(update.OldValues, nameof(SmsSettings.BirthdayDaysBefore)).GetInt32().ShouldBe(1);
        Value(update.NewValues, nameof(SmsSettings.BirthdayDaysBefore)).GetInt32().ShouldBe(3);
        Value(update.NewValues, nameof(SmsSettings.BirthdayEnabled)).GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Update_AsStaff_Returns403AndChangesNothing()
    {
        var (owner, ownerToken, _) = await OwnerClientAsync();
        var before = await GetOkAsync(owner, ownerToken);
        var (staff, staffToken) = await StaffClientAsync();

        using var response = await UpdateAsync(staff, staffToken, FullBody(before.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetOkAsync(owner, ownerToken)).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409ChangedConcurrently()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);
        (await UpdateAsync(client, token, Body(read.Version, enabled: true))).EnsureSuccessStatusCode().Dispose();

        using var response = await UpdateAsync(client, token, Body(read.Version, enabled: false));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Sms.ChangedConcurrently");
        (await GetOkAsync(client, token)).Enabled.ShouldBeTrue();
    }

    // ---- The daily runs follow the saved times (task 10.3) ----

    [Fact]
    public async Task Update_Saved_MovesTheRunsToTheSavedSettings()
    {
        var schedule = RecordingSchedule();
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        (await UpdateAsync(client, token, FullBody(read.Version))).EnsureSuccessStatusCode().Dispose();

        var applied = schedule.Applied.ShouldHaveSingleItem();
        applied.Enabled.ShouldBeTrue();
        applied.For(NotificationKind.SubscriptionExpiring).SendTime.ShouldBe(new TimeOnly(9, 0));
        applied.For(NotificationKind.PayableDue).SendTime.ShouldBe(new TimeOnly(22, 0));
        applied.For(NotificationKind.DailyReport).ShouldBe(new SmsKindSettings(true, null, new TimeOnly(22, 45)));

        // On, but no send time: the receipt goes at each payment, never by a daily run.
        applied.For(NotificationKind.SubscriptionReceipt).ShouldBe(new SmsKindSettings(true, null, null));
        applied.For(NotificationKind.Welcome).ShouldBe(new SmsKindSettings(true, null, null));
    }

    [Fact]
    public async Task Update_Refused_MovesNoRun()
    {
        var schedule = RecordingSchedule();
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, FullBody(read.Version + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        schedule.Applied.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_TwoSavesAtOnce_OneWinsAndTheOtherIsRefused()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        var responses = await Task.WhenAll(
            UpdateAsync(client, token, Body(read.Version, enabled: true)),
            UpdateAsync(client, token, Body(read.Version, enabled: true)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- Refusals, each under its own field ----

    [Fact]
    public async Task Update_KindOnWithAnEmptyField_Returns400SettingsIncompleteUnderTheSwitch()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, Body(read.Version, birthday: Kind(true, 3, null)));

        await ShouldHaveFieldErrorAsync(response, "birthday.enabled", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_ChequesOnWithoutTheOwnersNumber_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(
            client, token, Body(read.Version, payableDue: Kind(true, 3, "10:00:00"), ownerPhone: null));

        await ShouldHaveFieldErrorAsync(response, "payableDue.enabled", "Sms.SettingsIncomplete");
    }

    [Fact]
    public async Task Update_KindLeftOut_Returns400SettingsIncomplete()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, new
        {
            enabled = false,
            subscriptionExpiring = Off,
            lowSessions = Off,
            payableDue = Off,
            ownerPhone = (string?)null,
            dailyReport = ReportOff,
            subscriptionReceipt = ReceiptOff,
            welcome = WelcomeOff,
            broadcast = BroadcastOff,
            version = read.Version,
        });

        await ShouldHaveFieldErrorAsync(response, "birthday", "Sms.SettingsIncomplete");
    }

    [Theory]
    [InlineData("subscriptionExpiring", 31, null, "subscriptionExpiring.threshold", "Sms.SubscriptionExpiringDaysOutOfRange")]
    [InlineData("lowSessions", 11, null, "lowSessions.threshold", "Sms.LowSessionsOutOfRange")]
    [InlineData("birthday", 0, null, "birthday.threshold", "Sms.BirthdayDaysOutOfRange")]
    [InlineData("birthday", 8, null, "birthday.threshold", "Sms.BirthdayDaysOutOfRange")]
    [InlineData("payableDue", 31, null, "payableDue.threshold", "Sms.PayableDueDaysOutOfRange")]
    [InlineData("birthday", null, "22:15:00", "birthday.sendTime", "Sms.SendTimeOutOfRange")]
    [InlineData("lowSessions", null, "09:10:00", "lowSessions.sendTime", "Sms.SendTimeOutOfRange")]
    public async Task Update_FieldOutOfItsRange_Returns400WithTheFieldsCode(
        string kind, int? threshold, string? sendTime, string field, string code)
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);
        var refused = Kind(false, threshold, sendTime);

        using var response = await UpdateAsync(client, token, kind switch
        {
            "subscriptionExpiring" => Body(read.Version, subscriptionExpiring: refused),
            "lowSessions" => Body(read.Version, lowSessions: refused),
            "birthday" => Body(read.Version, birthday: refused),
            _ => Body(read.Version, payableDue: refused),
        });

        await ShouldHaveFieldErrorAsync(response, field, code);
    }

    // ---- Helpers ----

    /// <summary>The test host's schedule (GymApiFactory), emptied of what earlier tests saved.</summary>
    private RecordingSmsRunSchedule RecordingSchedule()
    {
        var schedule = Fixture.Services.GetRequiredService<ISmsRunSchedule>().ShouldBeOfType<RecordingSmsRunSchedule>();
        schedule.Clear();

        return schedule;
    }

    private static object Kind(bool enabled, int? threshold, string? sendTime) =>
        new { enabled, threshold, sendTime };

    private static object Body(
        uint version,
        bool enabled = false,
        object? subscriptionExpiring = null,
        object? lowSessions = null,
        object? birthday = null,
        object? payableDue = null,
        string? ownerPhone = null,
        object? dailyReport = null,
        object? subscriptionReceipt = null,
        object? welcome = null,
        object? debtReminder = null,
        object? absence = null,
        object? broadcast = null) =>
        new
        {
            enabled,
            subscriptionExpiring = subscriptionExpiring ?? Off,
            lowSessions = lowSessions ?? Off,
            birthday = birthday ?? Off,
            payableDue = payableDue ?? Off,
            ownerPhone,
            dailyReport = dailyReport ?? ReportOff,
            subscriptionReceipt = subscriptionReceipt ?? ReceiptOff,
            welcome = welcome ?? WelcomeOff,
            debtReminder = debtReminder ?? DebtReminderOff,
            absence = absence ?? AbsenceOff,
            broadcast = broadcast ?? BroadcastOff,
            version,
        };

    private static object FullBody(uint version) => Body(
        version,
        enabled: true,
        subscriptionExpiring: Kind(true, 7, "09:00:00"),
        lowSessions: Kind(true, 2, "09:15:00"),
        birthday: Kind(true, 1, "10:30:00"),
        payableDue: Kind(true, 3, "22:00:00"),
        ownerPhone: "09121234567",
        dailyReport: new { enabled = true, phone = "09351234567" },
        subscriptionReceipt: new { enabled = true },
        welcome: new { enabled = true },
        debtReminder: new { enabled = true, daysAfter = 30, sendTime = "11:00:00", minAmount = 200_000m },
        absence: Kind(true, 14, "11:30:00"));

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token, Guid UserId)> OwnerClientAsync()
    {
        var owner = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password), owner.Id);
    }

    private static async Task<SmsSettingsResponse> GetOkAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(body) };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<SmsSettingsResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SmsSettingsResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task ShouldHaveFieldErrorAsync(HttpResponseMessage response, string field, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().ShouldBe(code);
    }

    private static JsonElement Value(string? json, string property)
    {
        using var document = JsonDocument.Parse(json.ShouldNotBeNull());

        return document.RootElement.GetProperty(property).Clone();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

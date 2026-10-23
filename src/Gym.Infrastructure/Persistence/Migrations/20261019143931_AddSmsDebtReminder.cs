using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsDebtReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_send_times",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.AddColumn<int>(
                name: "debt_reminder_days_after",
                table: "sms_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "debt_reminder_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "debt_reminder_min_amount",
                table: "sms_settings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "debt_reminder_send_time",
                table: "sms_settings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "debt_since",
                table: "notifications",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                columns: new[] { "debt_reminder_days_after", "debt_reminder_enabled", "debt_reminder_min_amount", "debt_reminder_send_time" },
                values: new object[] { null, false, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_debt_reminder_days",
                table: "sms_settings",
                sql: "debt_reminder_days_after IS NULL OR debt_reminder_days_after BETWEEN 1 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_debt_reminder_min_amount",
                table: "sms_settings",
                sql: "debt_reminder_min_amount IS NULL OR debt_reminder_min_amount >= 1000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_debt_reminder_on_means_filled",
                table: "sms_settings",
                sql: "NOT debt_reminder_enabled OR (debt_reminder_days_after IS NOT NULL AND debt_reminder_send_time IS NOT NULL AND debt_reminder_min_amount IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_send_times",
                table: "sms_settings",
                sql: "(subscription_expiring_send_time IS NULL OR (subscription_expiring_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM subscription_expiring_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM subscription_expiring_send_time) = 0)) AND (low_sessions_send_time IS NULL OR (low_sessions_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM low_sessions_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM low_sessions_send_time) = 0)) AND (birthday_send_time IS NULL OR (birthday_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM birthday_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM birthday_send_time) = 0)) AND (payable_due_send_time IS NULL OR (payable_due_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM payable_due_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM payable_due_send_time) = 0)) AND (debt_reminder_send_time IS NULL OR (debt_reminder_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM debt_reminder_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM debt_reminder_send_time) = 0))");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_debt_reminder_per_member_and_debt",
                table: "notifications",
                columns: new[] { "member_id", "debt_since" },
                unique: true,
                filter: "kind = 'DebtReminder'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_debt_reminder_days",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_debt_reminder_min_amount",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_debt_reminder_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_send_times",
                table: "sms_settings");

            migrationBuilder.DropIndex(
                name: "ix_notifications_one_debt_reminder_per_member_and_debt",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "debt_reminder_days_after",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "debt_reminder_enabled",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "debt_reminder_min_amount",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "debt_reminder_send_time",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "debt_since",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_send_times",
                table: "sms_settings",
                sql: "(subscription_expiring_send_time IS NULL OR (subscription_expiring_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM subscription_expiring_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM subscription_expiring_send_time) = 0)) AND (low_sessions_send_time IS NULL OR (low_sessions_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM low_sessions_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM low_sessions_send_time) = 0)) AND (birthday_send_time IS NULL OR (birthday_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM birthday_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM birthday_send_time) = 0)) AND (payable_due_send_time IS NULL OR (payable_due_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM payable_due_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM payable_due_send_time) = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome')");
        }
    }
}

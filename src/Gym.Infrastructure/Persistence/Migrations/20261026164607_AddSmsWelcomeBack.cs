using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsWelcomeBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.AddColumn<bool>(
                name: "welcome_back_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                column: "welcome_back_enabled",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_welcome_back_per_member_and_absence",
                table: "notifications",
                columns: new[] { "member_id", "absent_since" },
                unique: true,
                filter: "kind = 'WelcomeBack'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind IN ('Absence', 'WelcomeBack') AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast', 'WelcomeBack')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_one_welcome_back_per_member_and_absence",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "welcome_back_enabled",
                table: "sms_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Absence' AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast')");
        }
    }
}

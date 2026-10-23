using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsWelcome : Migration
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
                name: "welcome_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                column: "welcome_enabled",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_welcome_per_member",
                table: "notifications",
                column: "member_id",
                unique: true,
                filter: "kind = 'Welcome'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_one_welcome_per_member",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "welcome_enabled",
                table: "sms_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt')");
        }
    }
}

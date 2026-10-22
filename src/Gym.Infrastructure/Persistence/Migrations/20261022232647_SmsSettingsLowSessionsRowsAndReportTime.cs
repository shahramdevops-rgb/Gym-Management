using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmsSettingsLowSessionsRowsAndReportTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_threshold",
                table: "sms_settings");

            migrationBuilder.RenameColumn(
                name: "low_sessions_threshold",
                table: "sms_settings",
                newName: "low_sessions_small_threshold");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "daily_report_send_time",
                table: "sms_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(22, 45, 0));

            migrationBuilder.AddColumn<int>(
                name: "low_sessions_large_threshold",
                table: "sms_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_sessions_medium_plan_max",
                table: "sms_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_sessions_medium_threshold",
                table: "sms_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_sessions_small_plan_max",
                table: "sms_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                columns: new[] { "daily_report_send_time", "low_sessions_large_threshold", "low_sessions_medium_plan_max", "low_sessions_medium_threshold", "low_sessions_small_plan_max" },
                values: new object[] { new TimeOnly(22, 45, 0), null, 15, null, 8 });

            // Carried forward (CLAUDE.md): the one number the Owner had chosen becomes all three rows',
            // capped at the new 4, so nothing changes until the Owner changes it (task 10.17).
            migrationBuilder.Sql(
                """
                UPDATE sms_settings
                SET low_sessions_small_threshold = LEAST(low_sessions_small_threshold, 4),
                    low_sessions_medium_threshold = LEAST(low_sessions_small_threshold, 4),
                    low_sessions_large_threshold = LEAST(low_sessions_small_threshold, 4)
                WHERE low_sessions_small_threshold IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_daily_report_send_time",
                table: "sms_settings",
                sql: "daily_report_send_time BETWEEN '08:00' AND '23:45' AND EXTRACT(MINUTE FROM daily_report_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM daily_report_send_time) = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings",
                sql: "NOT low_sessions_enabled OR (low_sessions_small_threshold IS NOT NULL AND low_sessions_send_time IS NOT NULL AND low_sessions_medium_threshold IS NOT NULL AND low_sessions_large_threshold IS NOT NULL AND low_sessions_small_plan_max IS NOT NULL AND low_sessions_medium_plan_max IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings",
                sql: "(low_sessions_small_plan_max IS NULL OR low_sessions_small_plan_max BETWEEN 5 AND 139) AND (low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max <= 140) AND (low_sessions_small_plan_max IS NULL OR low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max > low_sessions_small_plan_max)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_thresholds",
                table: "sms_settings",
                sql: "(low_sessions_small_threshold IS NULL OR low_sessions_small_threshold BETWEEN 1 AND 4) AND (low_sessions_medium_threshold IS NULL OR low_sessions_medium_threshold BETWEEN 1 AND 4) AND (low_sessions_large_threshold IS NULL OR low_sessions_large_threshold BETWEEN 1 AND 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_daily_report_send_time",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_thresholds",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "daily_report_send_time",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "low_sessions_large_threshold",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "low_sessions_medium_plan_max",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "low_sessions_medium_threshold",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "low_sessions_small_plan_max",
                table: "sms_settings");

            migrationBuilder.RenameColumn(
                name: "low_sessions_small_threshold",
                table: "sms_settings",
                newName: "low_sessions_threshold");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings",
                sql: "NOT low_sessions_enabled OR (low_sessions_threshold IS NOT NULL AND low_sessions_send_time IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_threshold",
                table: "sms_settings",
                sql: "low_sessions_threshold IS NULL OR low_sessions_threshold BETWEEN 1 AND 10");
        }
    }
}

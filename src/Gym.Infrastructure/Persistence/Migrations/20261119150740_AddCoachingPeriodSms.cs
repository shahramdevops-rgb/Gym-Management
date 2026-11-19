using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingPeriodSms : Migration
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

            migrationBuilder.AddColumn<int>(
                name: "coaching_period_ending_days",
                table: "sms_settings",
                type: "integer",
                nullable: false,

                // Hand-edited from 0: the one row starts at the 7 days the developer chose (task 15.7n).
                defaultValue: 7);

            migrationBuilder.AddColumn<bool>(
                name: "coaching_period_ending_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "coaching_period_ending_send_time",
                table: "sms_settings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_ends_on",
                table: "notifications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trainer_link_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                columns: new[] { "coaching_period_ending_days", "coaching_period_ending_enabled", "coaching_period_ending_send_time" },
                values: new object[] { 7, false, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_days",
                table: "sms_settings",
                sql: "coaching_period_ending_days BETWEEN 3 AND 14");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_on_means_filled",
                table: "sms_settings",
                sql: "NOT coaching_period_ending_enabled OR coaching_period_ending_send_time IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_send_time",
                table: "sms_settings",
                sql: "(coaching_period_ending_send_time IS NULL OR (coaching_period_ending_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM coaching_period_ending_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM coaching_period_ending_send_time) = 0))");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_period_ending_per_link_and_last_day",
                table: "notifications",
                columns: new[] { "trainer_link_id", "period_ends_on" },
                unique: true,
                filter: "kind = 'CoachingPeriodEnding'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind IN ('Absence', 'WelcomeBack') AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'TrainerRequest' AND member_id IS NOT NULL AND report_date IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'SessionCancelled' AND member_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'CoachingPeriodEnding' AND member_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast', 'WelcomeBack', 'TrainerRequest', 'SessionCancelled', 'CoachingPeriodEnding')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_link_only_for_coaching_period_ending",
                table: "notifications",
                sql: "(kind = 'CoachingPeriodEnding') = (trainer_link_id IS NOT NULL) AND (kind = 'CoachingPeriodEnding') = (period_ends_on IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_trainer_links_trainer_link_id",
                table: "notifications",
                column: "trainer_link_id",
                principalTable: "trainer_links",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_trainer_links_trainer_link_id",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_days",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_coaching_period_ending_send_time",
                table: "sms_settings");

            migrationBuilder.DropIndex(
                name: "ix_notifications_one_period_ending_per_link_and_last_day",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_link_only_for_coaching_period_ending",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "coaching_period_ending_days",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "coaching_period_ending_enabled",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "coaching_period_ending_send_time",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "period_ends_on",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "trainer_link_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind IN ('Absence', 'WelcomeBack') AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'TrainerRequest' AND member_id IS NOT NULL AND report_date IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'SessionCancelled' AND member_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast', 'WelcomeBack', 'TrainerRequest', 'SessionCancelled')");
        }
    }
}

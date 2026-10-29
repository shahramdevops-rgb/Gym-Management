using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerRequestSms : Migration
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
                name: "trainer_request_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "trainer_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                column: "trainer_request_enabled",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_trainer_request_per_member_trainer_and_day",
                table: "notifications",
                columns: new[] { "member_id", "trainer_id", "report_date" },
                unique: true,
                filter: "kind = 'TrainerRequest'");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_trainer_id",
                table: "notifications",
                column: "trainer_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind IN ('Absence', 'WelcomeBack') AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'TrainerRequest' AND member_id IS NOT NULL AND report_date IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast', 'WelcomeBack', 'TrainerRequest')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_trainer_only_for_trainer_request",
                table: "notifications",
                sql: "(kind = 'TrainerRequest') = (trainer_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_trainers_trainer_id",
                table: "notifications",
                column: "trainer_id",
                principalTable: "trainers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_trainers_trainer_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_one_trainer_request_per_member_trainer_and_day",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_trainer_id",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_trainer_only_for_trainer_request",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "trainer_request_enabled",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "trainer_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind IN ('Absence', 'WelcomeBack') AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast', 'WelcomeBack')");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsBroadcast : Migration
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
                name: "broadcast_enabled",
                table: "sms_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "broadcast_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sms_broadcasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    audience = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stop_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_broadcasts", x => x.id);
                    table.CheckConstraint("ck_sms_broadcasts_audience", "audience IN ('ActiveMembers', 'MembersWithUsablePlan')");
                    table.CheckConstraint("ck_sms_broadcasts_ended_when_not_sending", "(status = 'Sending' AND ended_at IS NULL) OR (status <> 'Sending' AND ended_at IS NOT NULL)");
                    table.CheckConstraint("ck_sms_broadcasts_status", "status IN ('Sending', 'Finished', 'Stopped')");
                    table.CheckConstraint("ck_sms_broadcasts_stop_reason", "(status = 'Stopped' AND stop_reason IN ('CreditExhausted', 'OutsideSendingHours', 'TurnedOff', 'Error', 'TooLong')) OR (status <> 'Stopped' AND stop_reason IS NULL)");
                    table.CheckConstraint("ck_sms_broadcasts_text_not_blank", "btrim(text) <> ''");
                });

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                column: "broadcast_enabled",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_per_broadcast_and_member",
                table: "notifications",
                columns: new[] { "broadcast_id", "member_id" },
                unique: true,
                filter: "broadcast_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Absence' AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND broadcast_id IS NULL) OR (kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast')");

            migrationBuilder.CreateIndex(
                name: "ix_sms_broadcasts_created_at",
                table: "sms_broadcasts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_sms_broadcasts_one_sending",
                table: "sms_broadcasts",
                column: "status",
                unique: true,
                filter: "status = 'Sending'");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_sms_broadcasts_broadcast_id",
                table: "notifications",
                column: "broadcast_id",
                principalTable: "sms_broadcasts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_sms_broadcasts_broadcast_id",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "sms_broadcasts");

            migrationBuilder.DropIndex(
                name: "ix_notifications_one_per_broadcast_and_member",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "broadcast_enabled",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "broadcast_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_event_matches_kind",
                table: "notifications",
                sql: "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'DailyReport' AND report_date IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND debt_since IS NULL AND absent_since IS NULL) OR (kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND absent_since IS NULL) OR (kind = 'Absence' AND member_id IS NOT NULL AND absent_since IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL AND debt_since IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence')");
        }
    }
}

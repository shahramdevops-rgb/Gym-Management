using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sms_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    subscription_expiring_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    subscription_expiring_days_before = table.Column<int>(type: "integer", nullable: true),
                    subscription_expiring_send_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    subscription_expiring_template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    low_sessions_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    low_sessions_threshold = table.Column<int>(type: "integer", nullable: true),
                    low_sessions_send_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    low_sessions_template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    birthday_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    birthday_days_before = table.Column<int>(type: "integer", nullable: true),
                    birthday_send_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    birthday_template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payable_due_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    payable_due_days_before = table.Column<int>(type: "integer", nullable: true),
                    payable_due_send_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    payable_due_template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    owner_phone = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_settings", x => x.id);
                    table.CheckConstraint("ck_sms_settings_birthday_days", "birthday_days_before IS NULL OR birthday_days_before BETWEEN 0 AND 7");
                    table.CheckConstraint("ck_sms_settings_birthday_on_means_filled", "NOT birthday_enabled OR (birthday_days_before IS NOT NULL AND birthday_send_time IS NOT NULL AND birthday_template_name IS NOT NULL)");
                    table.CheckConstraint("ck_sms_settings_low_sessions_on_means_filled", "NOT low_sessions_enabled OR (low_sessions_threshold IS NOT NULL AND low_sessions_send_time IS NOT NULL AND low_sessions_template_name IS NOT NULL)");
                    table.CheckConstraint("ck_sms_settings_low_sessions_threshold", "low_sessions_threshold IS NULL OR low_sessions_threshold BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_sms_settings_owner_phone", "owner_phone IS NULL OR owner_phone ~ '^\\+989[0-9]{9}$'");
                    table.CheckConstraint("ck_sms_settings_payable_due_days", "payable_due_days_before IS NULL OR payable_due_days_before BETWEEN 0 AND 30");
                    table.CheckConstraint("ck_sms_settings_payable_due_on_means_filled", "NOT payable_due_enabled OR (payable_due_days_before IS NOT NULL AND payable_due_send_time IS NOT NULL AND payable_due_template_name IS NOT NULL AND owner_phone IS NOT NULL)");
                    table.CheckConstraint("ck_sms_settings_send_times", "(subscription_expiring_send_time IS NULL OR (subscription_expiring_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM subscription_expiring_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM subscription_expiring_send_time) = 0)) AND (low_sessions_send_time IS NULL OR (low_sessions_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM low_sessions_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM low_sessions_send_time) = 0)) AND (birthday_send_time IS NULL OR (birthday_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM birthday_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM birthday_send_time) = 0)) AND (payable_due_send_time IS NULL OR (payable_due_send_time BETWEEN '08:00' AND '22:00' AND EXTRACT(MINUTE FROM payable_due_send_time) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM payable_due_send_time) = 0))");
                    table.CheckConstraint("ck_sms_settings_single_row", "id = '8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f'");
                    table.CheckConstraint("ck_sms_settings_subscription_expiring_days", "subscription_expiring_days_before IS NULL OR subscription_expiring_days_before BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_sms_settings_subscription_expiring_on_means_filled", "NOT subscription_expiring_enabled OR (subscription_expiring_days_before IS NOT NULL AND subscription_expiring_send_time IS NOT NULL AND subscription_expiring_template_name IS NOT NULL)");
                    table.CheckConstraint("ck_sms_settings_template_names", "(subscription_expiring_template_name IS NULL OR subscription_expiring_template_name ~ '^[A-Za-z0-9]+$') AND (low_sessions_template_name IS NULL OR low_sessions_template_name ~ '^[A-Za-z0-9]+$') AND (birthday_template_name IS NULL OR birthday_template_name ~ '^[A-Za-z0-9]+$') AND (payable_due_template_name IS NULL OR payable_due_template_name ~ '^[A-Za-z0-9]+$')");
                });

            migrationBuilder.InsertData(
                table: "sms_settings",
                columns: new[] { "id", "birthday_days_before", "birthday_enabled", "birthday_send_time", "birthday_template_name", "created_at", "created_by", "enabled", "low_sessions_enabled", "low_sessions_send_time", "low_sessions_template_name", "low_sessions_threshold", "owner_phone", "payable_due_days_before", "payable_due_enabled", "payable_due_send_time", "payable_due_template_name", "subscription_expiring_days_before", "subscription_expiring_enabled", "subscription_expiring_send_time", "subscription_expiring_template_name", "updated_at", "updated_by" },
                values: new object[] { new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"), null, false, null, null, new DateTimeOffset(new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, false, null, null, null, null, null, false, null, null, null, false, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sms_settings");
        }
    }
}

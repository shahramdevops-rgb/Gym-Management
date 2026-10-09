using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SendSmsAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_birthday_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_payable_due_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_subscription_expiring_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_template_names",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_template_name_not_blank",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_token_not_blank",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "birthday_template_name",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "low_sessions_template_name",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "payable_due_template_name",
                table: "sms_settings");

            migrationBuilder.DropColumn(
                name: "subscription_expiring_template_name",
                table: "sms_settings");

            // Carry every message written so far forward (BUSINESS_RULES.md §10, 1405/07/16): its text is
            // built from its kind and the values it kept, with the wording of docs/sms-texts.md as it
            // stands today, before the old columns go. A birthday took the «پیشاپیش» text when its
            // template was the early one.
            migrationBuilder.AddColumn<string>(
                name: "text",
                table: "notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE notifications SET text = CASE kind
                    WHEN 'SubscriptionExpiring' THEN
                        coalesce(token10, '') || ' عزیز، اشتراک شما در باشگاه پاسارگاد ' || token || ' به پایان می‌رسد.'
                    WHEN 'LowSessions' THEN
                        coalesce(token10, '') || ' عزیز، فقط ' || token || ' جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.'
                    WHEN 'Birthday' THEN CASE
                        WHEN template_name ILIKE '%early%' THEN
                            coalesce(token10, '') || ' عزیز، تولدتان در ' || token || ' را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد'
                        ELSE
                            coalesce(token10, '') || ' عزیز، امروز ' || token || ' روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد'
                        END
                    ELSE
                        'یادآوری ' || token || ': ' || coalesce(token2, '') || ' تومان، سررسید ' || coalesce(token3, '')
                        || '، به ' || coalesce(token20, '')
                    END;
                """);

            migrationBuilder.Sql("ALTER TABLE notifications ALTER COLUMN text SET NOT NULL;");

            migrationBuilder.DropColumn(
                name: "template_name",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "token",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "token10",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "token2",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "token20",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "token3",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_birthday_on_means_filled",
                table: "sms_settings",
                sql: "NOT birthday_enabled OR (birthday_days_before IS NOT NULL AND birthday_send_time IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings",
                sql: "NOT low_sessions_enabled OR (low_sessions_threshold IS NOT NULL AND low_sessions_send_time IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_payable_due_on_means_filled",
                table: "sms_settings",
                sql: "NOT payable_due_enabled OR (payable_due_days_before IS NOT NULL AND payable_due_send_time IS NOT NULL AND owner_phone IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_subscription_expiring_on_means_filled",
                table: "sms_settings",
                sql: "NOT subscription_expiring_enabled OR (subscription_expiring_days_before IS NOT NULL AND subscription_expiring_send_time IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_text_not_blank",
                table: "notifications",
                sql: "btrim(text) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to templates: the names come back as "freeText" and the values as "-", only so the
            // old check constraints pass. The text itself is lost.
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_birthday_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_payable_due_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_subscription_expiring_on_means_filled",
                table: "sms_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_text_not_blank",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "text",
                table: "notifications");

            migrationBuilder.AddColumn<string>(
                name: "birthday_template_name",
                table: "sms_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "low_sessions_template_name",
                table: "sms_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payable_due_template_name",
                table: "sms_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subscription_expiring_template_name",
                table: "sms_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_name",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "freeText");

            migrationBuilder.AddColumn<string>(
                name: "token",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "-");

            migrationBuilder.AddColumn<string>(
                name: "token10",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token2",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token20",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token3",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "sms_settings",
                keyColumn: "id",
                keyValue: new Guid("8b2e4f61-3c5a-4d7e-9f10-2a6b8c4d0e5f"),
                columns: new[] { "birthday_template_name", "low_sessions_template_name", "payable_due_template_name", "subscription_expiring_template_name" },
                values: new object[] { "freeText", "freeText", "freeText", "freeText" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_birthday_on_means_filled",
                table: "sms_settings",
                sql: "NOT birthday_enabled OR (birthday_days_before IS NOT NULL AND birthday_send_time IS NOT NULL AND birthday_template_name IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_on_means_filled",
                table: "sms_settings",
                sql: "NOT low_sessions_enabled OR (low_sessions_threshold IS NOT NULL AND low_sessions_send_time IS NOT NULL AND low_sessions_template_name IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_payable_due_on_means_filled",
                table: "sms_settings",
                sql: "NOT payable_due_enabled OR (payable_due_days_before IS NOT NULL AND payable_due_send_time IS NOT NULL AND payable_due_template_name IS NOT NULL AND owner_phone IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_subscription_expiring_on_means_filled",
                table: "sms_settings",
                sql: "NOT subscription_expiring_enabled OR (subscription_expiring_days_before IS NOT NULL AND subscription_expiring_send_time IS NOT NULL AND subscription_expiring_template_name IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_template_names",
                table: "sms_settings",
                sql: "(subscription_expiring_template_name IS NULL OR subscription_expiring_template_name ~ '^[A-Za-z0-9]+$') AND (low_sessions_template_name IS NULL OR low_sessions_template_name ~ '^[A-Za-z0-9]+$') AND (birthday_template_name IS NULL OR birthday_template_name ~ '^[A-Za-z0-9]+$') AND (payable_due_template_name IS NULL OR payable_due_template_name ~ '^[A-Za-z0-9]+$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_template_name_not_blank",
                table: "notifications",
                sql: "btrim(template_name) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_token_not_blank",
                table: "notifications",
                sql: "btrim(token) <> ''");
        }
    }
}

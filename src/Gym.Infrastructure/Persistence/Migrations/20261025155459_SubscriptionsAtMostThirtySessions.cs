using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionsAtMostThirtySessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_duration_days_for_sessions",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_duration_days_for_sessions",
                table: "subscriptions",
                sql: "is_single_session OR duration_days = CASE WHEN total_sessions <= 10 THEN 30 WHEN total_sessions <= 20 THEN 45 WHEN total_sessions <= 30 THEN 70 END");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "is_single_session OR total_sessions BETWEEN 5 AND 30 OR (cancelled_at IS NOT NULL AND total_sessions BETWEEN 5 AND 140)");

            // Carried forward (CLAUDE.md): a row size above the new ceiling comes down to it, so the
            // check below refuses nothing. The server held 8 and 15 on 1405/08/03, which this leaves
            // as they are (task 6.5.38).
            migrationBuilder.Sql(
                """
                UPDATE sms_settings
                SET low_sessions_small_plan_max = LEAST(low_sessions_small_plan_max, 29),
                    low_sessions_medium_plan_max = LEAST(low_sessions_medium_plan_max, 30)
                WHERE low_sessions_small_plan_max > 29 OR low_sessions_medium_plan_max > 30;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings",
                sql: "(low_sessions_small_plan_max IS NULL OR low_sessions_small_plan_max BETWEEN 5 AND 29) AND (low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max <= 30) AND (low_sessions_small_plan_max IS NULL OR low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max > low_sessions_small_plan_max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_duration_days_for_sessions",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_duration_days_for_sessions",
                table: "subscriptions",
                sql: "is_single_session OR duration_days = CASE WHEN total_sessions <= 10 THEN 30 WHEN total_sessions <= 20 THEN 45 WHEN total_sessions <= 140 THEN 70 END");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "is_single_session OR total_sessions BETWEEN 5 AND 140");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_low_sessions_plan_max",
                table: "sms_settings",
                sql: "(low_sessions_small_plan_max IS NULL OR low_sessions_small_plan_max BETWEEN 5 AND 139) AND (low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max <= 140) AND (low_sessions_small_plan_max IS NULL OR low_sessions_medium_plan_max IS NULL OR low_sessions_medium_plan_max > low_sessions_small_plan_max)");
        }
    }
}

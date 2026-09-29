using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanDaysFollowSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_duration_days_range",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_duration_days_for_sessions",
                table: "subscriptions",
                sql: "is_single_session OR duration_days = CASE WHEN total_sessions <= 10 THEN 30 WHEN total_sessions <= 20 THEN 45 WHEN total_sessions <= 140 THEN 70 END");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "is_single_session OR total_sessions BETWEEN 5 AND 140");
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_duration_days_range",
                table: "subscriptions",
                sql: "duration_days BETWEEN 1 AND 365");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "is_single_session OR total_sessions >= 5");
        }
    }
}

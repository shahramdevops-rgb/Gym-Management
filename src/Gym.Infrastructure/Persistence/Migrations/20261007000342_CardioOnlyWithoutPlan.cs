using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardioOnlyWithoutPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances",
                sql: "(member_id IS NULL AND subscription_id IS NULL) OR (member_id IS NOT NULL AND (subscription_id IS NOT NULL OR is_cardio_only))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances",
                sql: "(member_id IS NULL) = (subscription_id IS NULL)");
        }
    }
}

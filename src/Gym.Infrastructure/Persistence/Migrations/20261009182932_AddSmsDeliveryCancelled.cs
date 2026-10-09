using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsDeliveryCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_delivery",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_delivery",
                table: "notifications",
                sql: "delivery IS NULL OR delivery IN ('Delivered', 'NotDelivered', 'BlockedByReceiver', 'Cancelled')");

            // Carried forward, not refused: Kavenegar already gave back the cost of a blocked message
            // (BUSINESS_RULES.md §10), so one recorded before this migration costs nothing either.
            migrationBuilder.Sql("UPDATE notifications SET cost_rial = 0 WHERE delivery = 'BlockedByReceiver' AND cost_rial <> 0;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_refunded_has_no_cost",
                table: "notifications",
                sql: "delivery IS NULL OR delivery NOT IN ('BlockedByReceiver', 'Cancelled') OR cost_rial = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_delivery",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_refunded_has_no_cost",
                table: "notifications");

            // Before this migration Kavenegar's "cancelled" was recorded as not delivered.
            migrationBuilder.Sql("UPDATE notifications SET delivery = 'NotDelivered' WHERE delivery = 'Cancelled';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_delivery",
                table: "notifications",
                sql: "delivery IS NULL OR delivery IN ('Delivered', 'NotDelivered', 'BlockedByReceiver')");
        }
    }
}

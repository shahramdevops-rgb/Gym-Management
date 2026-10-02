using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_service_charges_charged_on",
                table: "service_charges",
                column: "charged_on");

            migrationBuilder.CreateIndex(
                name: "ix_payments_paid_at",
                table: "payments",
                column: "paid_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_charges_charged_on",
                table: "service_charges");

            migrationBuilder.DropIndex(
                name: "ix_payments_paid_at",
                table: "payments");
        }
    }
}

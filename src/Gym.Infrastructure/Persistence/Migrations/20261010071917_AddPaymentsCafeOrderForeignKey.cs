using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsCafeOrderForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payments_cafe_order_id",
                table: "payments",
                column: "cafe_order_id");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_cafe_orders_cafe_order_id",
                table: "payments",
                column: "cafe_order_id",
                principalTable: "cafe_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_cafe_orders_cafe_order_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_cafe_order_id",
                table: "payments");
        }
    }
}

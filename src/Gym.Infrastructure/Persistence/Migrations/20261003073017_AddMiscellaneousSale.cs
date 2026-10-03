using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMiscellaneousSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_charges_one_live_per_visit_and_kind",
                table: "service_charges");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "service_charges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "service_charges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                table: "service_charges",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_one_live_per_visit_and_kind",
                table: "service_charges",
                columns: new[] { "attendance_id", "kind" },
                unique: true,
                filter: "voided_at IS NULL AND kind = 'Cardio'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_charges_miscellaneous",
                table: "service_charges",
                sql: "(kind = 'Miscellaneous') = (description IS NOT NULL) AND (description IS NULL) = (quantity IS NULL) AND (description IS NULL) = (unit_price IS NULL) AND (quantity IS NULL OR (quantity BETWEEN 1 AND 999 AND unit_price > 0 AND amount = unit_price * quantity))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_charges_one_live_per_visit_and_kind",
                table: "service_charges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_charges_miscellaneous",
                table: "service_charges");

            migrationBuilder.DropColumn(
                name: "description",
                table: "service_charges");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "service_charges");

            migrationBuilder.DropColumn(
                name: "unit_price",
                table: "service_charges");

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_one_live_per_visit_and_kind",
                table: "service_charges",
                columns: new[] { "attendance_id", "kind" },
                unique: true,
                filter: "voided_at IS NULL");
        }
    }
}

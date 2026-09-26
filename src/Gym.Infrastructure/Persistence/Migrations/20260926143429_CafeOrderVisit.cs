using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CafeOrderVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attendance_id",
                table: "cafe_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cafe_orders_attendance_id",
                table: "cafe_orders",
                column: "attendance_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cafe_orders_visit_has_member",
                table: "cafe_orders",
                sql: "attendance_id IS NULL OR member_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_cafe_orders_attendances_attendance_id",
                table: "cafe_orders",
                column: "attendance_id",
                principalTable: "attendances",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cafe_orders_attendances_attendance_id",
                table: "cafe_orders");

            migrationBuilder.DropIndex(
                name: "ix_cafe_orders_attendance_id",
                table: "cafe_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cafe_orders_visit_has_member",
                table: "cafe_orders");

            migrationBuilder.DropColumn(
                name: "attendance_id",
                table: "cafe_orders");
        }
    }
}

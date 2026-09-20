using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceAutoClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "auto_closed_at",
                table: "attendances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_auto_close",
                table: "attendances",
                sql: "auto_closed_at IS NULL OR checked_out_at = auto_closed_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_one_close_reason",
                table: "attendances",
                sql: "cancelled_at IS NULL OR auto_closed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_auto_close",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_one_close_reason",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "auto_closed_at",
                table: "attendances");
        }
    }
}

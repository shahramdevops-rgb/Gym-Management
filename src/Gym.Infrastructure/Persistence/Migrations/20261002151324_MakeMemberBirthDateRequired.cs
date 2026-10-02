using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeMemberBirthDateRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_members_birth_date_range",
                table: "members");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "birth_date",
                table: "members",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_members_birth_date_range",
                table: "members",
                sql: "birth_date >= DATE '1900-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_members_birth_date_range",
                table: "members");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "birth_date",
                table: "members",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_members_birth_date_range",
                table: "members",
                sql: "birth_date IS NULL OR birth_date >= DATE '1900-01-01'");
        }
    }
}

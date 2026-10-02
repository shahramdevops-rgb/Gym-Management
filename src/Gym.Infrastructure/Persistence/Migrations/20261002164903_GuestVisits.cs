using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cafe_orders_visit_has_member",
                table: "cafe_orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "subscription_id",
                table: "attendances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "member_id",
                table: "attendances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "guest_name",
                table: "attendances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_guest_name_not_blank",
                table: "attendances",
                sql: "guest_name IS NULL OR btrim(guest_name) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_member_or_guest",
                table: "attendances",
                sql: "(member_id IS NULL) <> (guest_name IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances",
                sql: "(member_id IS NULL) = (subscription_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_guest_name_not_blank",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_member_or_guest",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_subscription_with_member",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "guest_name",
                table: "attendances");

            migrationBuilder.AlterColumn<Guid>(
                name: "subscription_id",
                table: "attendances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "member_id",
                table: "attendances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_cafe_orders_visit_has_member",
                table: "cafe_orders",
                sql: "attendance_id IS NULL OR member_id IS NOT NULL");
        }
    }
}

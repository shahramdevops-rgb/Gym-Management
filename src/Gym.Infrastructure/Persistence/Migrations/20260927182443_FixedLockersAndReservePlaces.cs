using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedLockersAndReservePlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_lockers_number_positive",
                table: "lockers");

            migrationBuilder.AddColumn<short>(
                name: "reserve_slot",
                table: "attendances",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "attendances",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.InsertData(
                table: "lockers",
                columns: new[] { "id", "created_at", "created_by", "is_out_of_service", "number", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("10c4e700-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 1, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 2, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000003"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 3, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 4, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000005"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 5, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000006"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 6, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000007"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 7, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000008"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 8, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000009"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 9, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000010"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 10, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000011"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 11, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000012"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 12, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000013"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 13, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000014"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 14, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000015"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 15, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000016"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 16, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000017"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 17, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000018"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 18, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000019"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 19, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000020"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 20, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000021"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 21, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000022"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 22, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000023"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 23, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000024"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 24, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000025"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 25, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000026"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 26, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000027"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 27, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000028"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 28, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000029"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 29, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000030"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 30, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000031"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 31, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000032"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 32, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000033"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 33, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000034"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 34, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000035"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 35, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000036"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 36, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000037"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 37, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000038"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 38, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000039"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 39, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000040"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 40, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000041"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 41, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000042"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 42, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000043"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 43, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000044"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 44, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000045"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 45, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000046"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 46, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000047"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 47, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000048"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 48, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000049"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 49, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000050"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 50, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000051"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 51, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000052"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 52, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000053"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 53, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000054"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 54, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000055"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 55, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000056"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 56, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000057"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 57, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000058"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 58, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000059"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 59, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000060"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 60, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000061"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 61, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000062"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 62, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000063"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 63, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000064"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 64, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000065"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 65, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000066"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 66, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000067"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 67, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000068"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 68, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000069"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 69, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000070"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 70, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000071"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 71, null, null },
                    { new Guid("10c4e700-0000-7000-8000-000000000072"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, 72, null, null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_lockers_number_range",
                table: "lockers",
                sql: "number BETWEEN 1 AND 72");

            migrationBuilder.CreateIndex(
                name: "ix_attendances_one_open_per_reserve_slot",
                table: "attendances",
                column: "reserve_slot",
                unique: true,
                filter: "checked_out_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_open_holds_one_place",
                table: "attendances",
                sql: "checked_out_at IS NOT NULL OR ((locker_id IS NULL) <> (reserve_slot IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_reserve_slot_range",
                table: "attendances",
                sql: "reserve_slot BETWEEN 1 AND 15");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_lockers_number_range",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "ix_attendances_one_open_per_reserve_slot",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_open_holds_one_place",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_reserve_slot_range",
                table: "attendances");

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000011"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000012"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000013"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000014"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000015"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000016"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000017"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000018"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000019"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000020"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000021"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000022"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000023"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000024"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000025"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000026"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000027"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000028"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000029"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000030"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000031"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000032"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000033"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000034"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000035"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000036"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000037"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000038"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000039"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000040"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000041"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000042"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000043"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000044"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000045"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000046"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000047"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000048"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000049"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000050"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000051"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000052"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000053"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000054"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000055"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000056"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000057"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000058"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000059"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000060"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000061"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000062"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000063"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000064"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000065"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000066"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000067"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000068"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000069"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000070"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000071"));

            migrationBuilder.DeleteData(
                table: "lockers",
                keyColumn: "id",
                keyValue: new Guid("10c4e700-0000-7000-8000-000000000072"));

            migrationBuilder.DropColumn(
                name: "reserve_slot",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "attendances");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lockers_number_positive",
                table: "lockers",
                sql: "number >= 1");
        }
    }
}

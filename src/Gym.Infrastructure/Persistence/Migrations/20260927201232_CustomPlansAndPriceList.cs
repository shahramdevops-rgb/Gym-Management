using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomPlansAndPriceList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_subscriptions_plans_plan_id",
                table: "subscriptions");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_plan_id",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_used_sessions",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "plan_id",
                table: "subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "total_sessions",
                table: "subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    single_visit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_lists", x => x.id);
                    table.CheckConstraint("ck_price_lists_prices_not_negative", "(session_price IS NULL OR session_price >= 0) AND (single_visit_price IS NULL OR single_visit_price >= 0)");
                    table.CheckConstraint("ck_price_lists_single_row", "id = '3f1c9a52-7d4e-4b8a-9c21-5e6f7a8b9c0d'");
                });

            migrationBuilder.InsertData(
                table: "price_lists",
                columns: new[] { "id", "created_at", "created_by", "session_price", "single_visit_price", "updated_at", "updated_by" },
                values: new object[] { new Guid("3f1c9a52-7d4e-4b8a-9c21-5e6f7a8b9c0d"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "is_single_session OR total_sessions >= 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_used_sessions",
                table: "subscriptions",
                sql: "used_sessions >= 0 AND used_sessions <= total_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_lists");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_used_sessions",
                table: "subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "total_sessions",
                table: "subscriptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                table: "subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    session_count = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.CheckConstraint("ck_plans_duration_days_range", "duration_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_plans_name_not_blank", "btrim(name) <> ''");
                    table.CheckConstraint("ck_plans_price_not_negative", "price >= 0");
                    table.CheckConstraint("ck_plans_session_count_range", "session_count IS NULL OR session_count BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_plans_single_session_shape", "kind <> 'SingleSession' OR (duration_days = 1 AND session_count = 1)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_id",
                table: "subscriptions",
                column: "plan_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_total_sessions_range",
                table: "subscriptions",
                sql: "total_sessions IS NULL OR total_sessions BETWEEN 1 AND 365");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_used_sessions",
                table: "subscriptions",
                sql: "used_sessions >= 0 AND (total_sessions IS NULL OR used_sessions <= total_sessions)");

            migrationBuilder.CreateIndex(
                name: "ix_plans_normalized_name",
                table: "plans",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_plans_single_session",
                table: "plans",
                column: "kind",
                unique: true,
                filter: "kind = 'SingleSession'");

            migrationBuilder.AddForeignKey(
                name: "fk_subscriptions_plans_plan_id",
                table: "subscriptions",
                column: "plan_id",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

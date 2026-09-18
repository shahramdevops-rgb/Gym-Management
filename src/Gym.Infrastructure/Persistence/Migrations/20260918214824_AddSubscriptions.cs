using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    total_sessions = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    used_sessions = table.Column<int>(type: "integer", nullable: false),
                    frozen_since = table.Column<DateOnly>(type: "date", nullable: true),
                    total_frozen_days = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_cancellation", "(cancelled_at IS NULL) = (cancellation_reason IS NULL)");
                    table.CheckConstraint("ck_subscriptions_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_subscriptions_duration_days_range", "duration_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_subscriptions_price_not_negative", "price >= 0");
                    table.CheckConstraint("ck_subscriptions_total_frozen_days", "total_frozen_days >= 0");
                    table.CheckConstraint("ck_subscriptions_total_sessions_range", "total_sessions IS NULL OR total_sessions BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_subscriptions_used_sessions", "used_sessions >= 0 AND (total_sessions IS NULL OR used_sessions <= total_sessions)");
                    table.ForeignKey(
                        name: "fk_subscriptions_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_member_id_end_date",
                table: "subscriptions",
                columns: new[] { "member_id", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_id",
                table: "subscriptions",
                column: "plan_id");

            // BUSINESS_RULES.md §4: a member never has two subscriptions covering the same date.
            // EF Core cannot model an exclusion constraint, so it is written by hand (the name is
            // SubscriptionConstraints.NoOverlap). '[]' makes both dates inclusive, like EndDate.
            // Cancelled subscriptions cover no dates. Checked immediately by default; DEFERRABLE
            // lets a transaction that moves several subscriptions at once (unfreeze, task 4.3)
            // run SET CONSTRAINTS ... DEFERRED and pass through a moment of overlap. Deferred by
            // default, parallel sales deadlocked at commit, each waiting for the other's row.
            migrationBuilder.Sql(
                """
                ALTER TABLE subscriptions
                    ADD CONSTRAINT ex_subscriptions_no_overlap
                    EXCLUDE USING gist (member_id WITH =, daterange(start_date, end_date, '[]') WITH &&)
                    WHERE (cancelled_at IS NULL)
                    DEFERRABLE INITIALLY IMMEDIATE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The audit log table, plus the trigger that makes it append-only. The trigger is written
    /// by hand below: EF Core has no model API for triggers, so it is not in the snapshot and a
    /// future migration will neither see nor remove it.
    /// </summary>
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_action", "action IN ('Insert', 'Update', 'Delete')");
                    table.CheckConstraint("ck_audit_logs_values_match_action", "(action = 'Insert' AND old_values IS NULL AND new_values IS NOT NULL) OR (action = 'Update' AND old_values IS NOT NULL AND new_values IS NOT NULL) OR (action = 'Delete' AND old_values IS NOT NULL AND new_values IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            // Append-only, enforced by the database (BUSINESS_RULES.md §11): no UPDATE and no
            // DELETE, whoever connects and however they got there. Row-level triggers do not fire
            // for TRUNCATE, which stays available to the test harness (Respawn) and to a
            // deliberate, documented clean-up by a database administrator.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_logs_reject_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER audit_logs_append_only
                BEFORE UPDATE OR DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION audit_logs_reject_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER audit_logs_append_only ON audit_logs;
                DROP FUNCTION audit_logs_reject_change();
                """);

            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}

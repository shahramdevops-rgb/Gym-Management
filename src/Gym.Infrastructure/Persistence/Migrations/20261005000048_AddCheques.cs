using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    passed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cheques", x => x.id);
                    table.CheckConstraint("ck_cheques_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_cheques_cancelled", "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");
                    table.CheckConstraint("ck_cheques_description_not_blank", "btrim(description) <> ''");
                    table.CheckConstraint("ck_cheques_not_passed_and_cancelled", "passed_at IS NULL OR cancelled_at IS NULL");
                    table.CheckConstraint("ck_cheques_passed", "(passed_at IS NULL) = (passed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_cheques_payee_not_blank", "btrim(payee) <> ''");
                    table.ForeignKey(
                        name: "fk_cheques_asp_net_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cheques_asp_net_users_passed_by_user_id",
                        column: x => x.passed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cheques_asp_net_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cheques_cancelled_by_user_id",
                table: "cheques",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cheques_due_date",
                table: "cheques",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_cheques_passed_by_user_id",
                table: "cheques",
                column: "passed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cheques_registered_by_user_id",
                table: "cheques",
                column: "registered_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cheques");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cheques");

            migrationBuilder.AddColumn<Guid>(
                name: "payable_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: true),
                    installment_count = table.Column<int>(type: "integer", nullable: true),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_payables", x => x.id);
                    table.CheckConstraint("ck_payables_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_payables_cancelled", "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");
                    table.CheckConstraint("ck_payables_description_not_blank", "btrim(description) <> ''");
                    table.CheckConstraint("ck_payables_installment_numbers", "(kind <> 'Installment' AND installment_number IS NULL AND installment_count IS NULL) OR (kind = 'Installment' AND installment_number IS NOT NULL AND installment_count IS NOT NULL AND installment_number >= 1 AND installment_number <= installment_count AND installment_count <= 360)");
                    table.CheckConstraint("ck_payables_kind", "kind IN ('Cheque', 'Installment')");
                    table.CheckConstraint("ck_payables_not_paid_and_cancelled", "paid_at IS NULL OR cancelled_at IS NULL");
                    table.CheckConstraint("ck_payables_paid", "(paid_at IS NULL) = (paid_by_user_id IS NULL)");
                    table.CheckConstraint("ck_payables_payee_not_blank", "btrim(payee) <> ''");
                    table.ForeignKey(
                        name: "fk_payables_asp_net_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payables_asp_net_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payables_asp_net_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payables_expense_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "expense_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_payable_id_standing",
                table: "expenses",
                column: "payable_id",
                unique: true,
                filter: "voided_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payables_cancelled_by_user_id",
                table: "payables",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payables_category_id",
                table: "payables",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_payables_due_date",
                table: "payables",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_payables_paid_by_user_id",
                table: "payables",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payables_registered_by_user_id",
                table: "payables",
                column: "registered_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_expenses_payables_payable_id",
                table: "expenses",
                column: "payable_id",
                principalTable: "payables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expenses_payables_payable_id",
                table: "expenses");

            migrationBuilder.DropTable(
                name: "payables");

            migrationBuilder.DropIndex(
                name: "ix_expenses_payable_id_standing",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "payable_id",
                table: "expenses");

            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    passed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    passed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
    }
}

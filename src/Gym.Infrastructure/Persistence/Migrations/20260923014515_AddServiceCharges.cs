using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_one_target",
                table: "payments");

            migrationBuilder.AddColumn<Guid>(
                name: "service_charge_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    charged_on = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_charges", x => x.id);
                    table.CheckConstraint("ck_service_charges_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_service_charges_void", "(voided_at IS NULL) = (void_reason IS NULL) AND (voided_at IS NULL) = (voided_by_user_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_service_charges_asp_net_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_charges_asp_net_users_voided_by_user_id",
                        column: x => x.voided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_charges_attendances_attendance_id",
                        column: x => x.attendance_id,
                        principalTable: "attendances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_charges_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_service_charge_id",
                table: "payments",
                column: "service_charge_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_one_target",
                table: "payments",
                sql: "num_nonnulls(subscription_id, cafe_order_id, service_charge_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_member_id",
                table: "service_charges",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_one_live_per_visit_and_kind",
                table: "service_charges",
                columns: new[] { "attendance_id", "kind" },
                unique: true,
                filter: "voided_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_recorded_by_user_id",
                table: "service_charges",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_charges_voided_by_user_id",
                table: "service_charges",
                column: "voided_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_service_charges_service_charge_id",
                table: "payments",
                column: "service_charge_id",
                principalTable: "service_charges",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_service_charges_service_charge_id",
                table: "payments");

            migrationBuilder.DropTable(
                name: "service_charges");

            migrationBuilder.DropIndex(
                name: "ix_payments_service_charge_id",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_one_target",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "service_charge_id",
                table: "payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_one_target",
                table: "payments",
                sql: "num_nonnulls(subscription_id, cafe_order_id) = 1");
        }
    }
}

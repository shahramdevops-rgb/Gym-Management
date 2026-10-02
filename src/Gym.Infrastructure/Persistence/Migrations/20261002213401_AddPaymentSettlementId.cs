using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSettlementId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "settlement_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            // Carries forward the «تسویه یکجا» rows written before this column existed. Both settle
            // handlers gave every row of one handover the same moment, method and staff member, and
            // nothing else writes two payments at one microsecond, so two or more such payments are
            // exactly one settlement. A payment taken on its own stays null.
            //
            // MATERIALIZED is load-bearing: as a plain subquery, Postgres may rescan it once per row
            // it joins and call gen_random_uuid() again each time, giving every row its own id.
            // A materialized CTE is computed once, so a group's rows all read the same id.
            migrationBuilder.Sql("""
                WITH g AS MATERIALIZED (
                    SELECT paid_at, received_by_user_id, method, gen_random_uuid() AS settlement_id
                    FROM payments
                    WHERE kind = 'Payment'
                    GROUP BY paid_at, received_by_user_id, method
                    HAVING count(*) > 1
                )
                UPDATE payments AS p
                SET settlement_id = g.settlement_id
                FROM g
                WHERE p.kind = 'Payment'
                  AND p.paid_at = g.paid_at
                  AND p.received_by_user_id = g.received_by_user_id
                  AND p.method = g.method;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_payments_settlement_id",
                table: "payments",
                column: "settlement_id",
                filter: "settlement_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_settlement_payment_only",
                table: "payments",
                sql: "settlement_id IS NULL OR kind = 'Payment'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_settlement_id",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_settlement_payment_only",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "settlement_id",
                table: "payments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSignInCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sign_in_code_sends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_send_again = table.Column<bool>(type: "boolean", nullable: false),
                    provider_message_id = table.Column<long>(type: "bigint", nullable: true),
                    cost_rial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sign_in_code_sends", x => x.id);
                    table.CheckConstraint("ck_sign_in_code_sends_cost_not_negative", "cost_rial IS NULL OR cost_rial >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sign_in_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    code_seed = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_provider_message_id = table.Column<long>(type: "bigint", nullable: true),
                    wrong_tries = table.Column<int>(type: "integer", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sign_in_codes", x => x.id);
                    table.CheckConstraint("ck_sign_in_codes_expires_after_issue", "expires_at > issued_at");
                    table.CheckConstraint("ck_sign_in_codes_sent_after_issue", "last_sent_at >= issued_at");
                    table.CheckConstraint("ck_sign_in_codes_used_after_issue", "used_at IS NULL OR used_at >= issued_at");
                    table.CheckConstraint("ck_sign_in_codes_wrong_tries_in_range", "wrong_tries >= 0 AND wrong_tries <= 5");
                });

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_code_sends_phone_number_sent_at",
                table: "sign_in_code_sends",
                columns: new[] { "phone_number", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_code_sends_sent_at",
                table: "sign_in_code_sends",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_codes_phone_number",
                table: "sign_in_codes",
                column: "phone_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sign_in_code_sends");

            migrationBuilder.DropTable(
                name: "sign_in_codes");
        }
    }
}

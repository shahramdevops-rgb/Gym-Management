using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "wrong_tries_on",
                table: "sign_in_codes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "wrong_tries_that_day",
                table: "sign_in_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "site_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_accounts_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_site_refresh_tokens_replaced_only_when_rotated", "replaced_by_token_id IS NULL OR revoked_reason = 'Rotated'");
                    table.CheckConstraint("ck_site_refresh_tokens_revocation_complete", "(revoked_at IS NULL) = (revoked_reason IS NULL)");
                    table.ForeignKey(
                        name: "fk_site_refresh_tokens_site_accounts_site_account_id",
                        column: x => x.site_account_id,
                        principalTable: "site_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_site_refresh_tokens_site_refresh_tokens_replaced_by_token_id",
                        column: x => x.replaced_by_token_id,
                        principalTable: "site_refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sign_in_codes_wrong_tries_counted_with_their_day",
                table: "sign_in_codes",
                sql: "(wrong_tries_on IS NULL AND wrong_tries_that_day = 0) OR (wrong_tries_on IS NOT NULL AND wrong_tries_that_day > 0)");

            migrationBuilder.CreateIndex(
                name: "ix_site_accounts_member_id",
                table: "site_accounts",
                column: "member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_refresh_tokens_family_id",
                table: "site_refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_site_refresh_tokens_replaced_by_token_id",
                table: "site_refresh_tokens",
                column: "replaced_by_token_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_refresh_tokens_site_account_id",
                table: "site_refresh_tokens",
                column: "site_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_site_refresh_tokens_token_hash",
                table: "site_refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_refresh_tokens");

            migrationBuilder.DropTable(
                name: "site_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sign_in_codes_wrong_tries_counted_with_their_day",
                table: "sign_in_codes");

            migrationBuilder.DropColumn(
                name: "wrong_tries_on",
                table: "sign_in_codes");

            migrationBuilder.DropColumn(
                name: "wrong_tries_that_day",
                table: "sign_in_codes");
        }
    }
}

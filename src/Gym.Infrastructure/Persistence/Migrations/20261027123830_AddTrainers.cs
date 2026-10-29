using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "member_id",
                table: "site_accounts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "trainer_id",
                table: "site_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trainers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trainers", x => x.id);
                    table.CheckConstraint("ck_trainers_full_name_not_blank", "btrim(full_name) <> ''");
                    table.CheckConstraint("ck_trainers_phone_number_e164", "phone_number ~ '^\\+[0-9]{7,15}$'");
                });

            migrationBuilder.CreateTable(
                name: "trainer_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trainer_periods", x => x.id);
                    table.CheckConstraint("ck_trainer_periods_ended_on_not_before_started_on", "ended_on IS NULL OR ended_on >= started_on");
                    table.ForeignKey(
                        name: "fk_trainer_periods_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_accounts_trainer_id",
                table: "site_accounts",
                column: "trainer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trainer_periods_trainer_id_open",
                table: "trainer_periods",
                column: "trainer_id",
                unique: true,
                filter: "ended_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trainers_phone_number",
                table: "trainers",
                column: "phone_number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_site_accounts_trainers_trainer_id",
                table: "site_accounts",
                column: "trainer_id",
                principalTable: "trainers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_site_accounts_trainers_trainer_id",
                table: "site_accounts");

            migrationBuilder.DropTable(
                name: "trainer_periods");

            migrationBuilder.DropTable(
                name: "trainers");

            migrationBuilder.DropIndex(
                name: "ix_site_accounts_trainer_id",
                table: "site_accounts");

            migrationBuilder.DropColumn(
                name: "trainer_id",
                table: "site_accounts");

            migrationBuilder.AlterColumn<Guid>(
                name: "member_id",
                table: "site_accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

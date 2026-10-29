using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerRequestsAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trainer_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trainer_requests", x => x.id);
                    table.CheckConstraint("ck_trainer_requests_status", "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled', 'Replaced', 'TrainerStopped')");
                    table.ForeignKey(
                        name: "fk_trainer_requests_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trainer_requests_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trainer_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trainer_links", x => x.id);
                    table.CheckConstraint("ck_trainer_links_end_reason", "end_reason IS NULL OR end_reason IN ('ByMember', 'ByTrainer', 'NewTrainer', 'TrainerStopped')");
                    table.CheckConstraint("ck_trainer_links_end_reason_with_ended_on", "(ended_on IS NULL) = (end_reason IS NULL)");
                    table.CheckConstraint("ck_trainer_links_ended_on_not_before_started_on", "ended_on IS NULL OR ended_on >= started_on");
                    table.ForeignKey(
                        name: "fk_trainer_links_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trainer_links_trainer_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "trainer_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trainer_links_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trainer_links_member_id_open",
                table: "trainer_links",
                column: "member_id",
                unique: true,
                filter: "ended_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trainer_links_request_id",
                table: "trainer_links",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trainer_links_trainer_id_ended_on",
                table: "trainer_links",
                columns: new[] { "trainer_id", "ended_on" });

            migrationBuilder.CreateIndex(
                name: "ix_trainer_requests_member_id_pending",
                table: "trainer_requests",
                column: "member_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_trainer_requests_trainer_id_status",
                table: "trainer_requests",
                columns: new[] { "trainer_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trainer_links");

            migrationBuilder.DropTable(
                name: "trainer_requests");
        }
    }
}

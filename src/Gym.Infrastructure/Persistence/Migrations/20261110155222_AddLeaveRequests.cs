using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requested_on = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_requests", x => x.id);
                    table.CheckConstraint("ck_leave_requests_asked_before_the_date", "requested_on < date");
                    table.CheckConstraint("ck_leave_requests_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_leave_requests_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");
                    table.CheckConstraint("ck_leave_requests_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");
                    table.CheckConstraint("ck_leave_requests_status", "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled')");
                    table.CheckConstraint("ck_leave_requests_within_hours", "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");
                    table.ForeignKey(
                        name: "fk_leave_requests_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_trainer_links_link_id",
                        column: x => x.link_id,
                        principalTable: "trainer_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_link_id",
                table: "leave_requests",
                column: "link_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_trainer_id_status_date",
                table: "leave_requests",
                columns: new[] { "trainer_id", "status", "date" });

            migrationBuilder.CreateIndex(
                name: "ux_leave_requests_one_live_per_place",
                table: "leave_requests",
                columns: new[] { "member_id", "date", "start_time", "end_time" },
                unique: true,
                filter: "status IN ('Pending', 'Accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_requests");
        }
    }
}

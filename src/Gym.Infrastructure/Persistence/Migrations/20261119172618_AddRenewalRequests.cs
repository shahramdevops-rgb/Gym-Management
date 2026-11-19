using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "renewal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateOnly>(type: "date", nullable: false),
                    period_ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    answered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_renewal_requests", x => x.id);
                    table.CheckConstraint("ck_renewal_requests_answered_on_not_before_requested_on", "answered_on IS NULL OR answered_on >= requested_on");
                    table.CheckConstraint("ck_renewal_requests_answered_on_with_answer", "(answered_on IS NOT NULL) = (status IN ('Renewed', 'Declined'))");
                    table.CheckConstraint("ck_renewal_requests_status", "status IN ('Pending', 'Renewed', 'Declined', 'Cancelled', 'LinkEnded')");
                    table.ForeignKey(
                        name: "fk_renewal_requests_trainer_links_link_id",
                        column: x => x.link_id,
                        principalTable: "trainer_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_renewal_requests_link_id_created_at",
                table: "renewal_requests",
                columns: new[] { "link_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_renewal_requests_one_pending_per_link",
                table: "renewal_requests",
                column: "link_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "renewal_requests");
        }
    }
}

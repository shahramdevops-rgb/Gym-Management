using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coaching_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coaching_periods", x => x.id);
                    table.CheckConstraint("ck_coaching_periods_at_most_84_days", "ends_on - starts_on < 84");
                    table.CheckConstraint("ck_coaching_periods_ends_on_not_before_starts_on", "ends_on >= starts_on");
                    table.ForeignKey(
                        name: "fk_coaching_periods_trainer_links_link_id",
                        column: x => x.link_id,
                        principalTable: "trainer_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coaching_periods_link_id_ends_on",
                table: "coaching_periods",
                columns: new[] { "link_id", "ends_on" });

            // BUSINESS_RULES.md §15 (15.7h): two periods of a link never share a day, so a renewal
            // starts after the last day. EF Core cannot declare an exclusion constraint; btree_gist is
            // already installed. Dropped with its table in Down. The links made before 15.7h get no
            // row: they have no period until the trainer types the days left.
            migrationBuilder.Sql(
                """
                ALTER TABLE coaching_periods
                    ADD CONSTRAINT ex_coaching_periods_link_id_dates
                    EXCLUDE USING gist (link_id WITH =, daterange(starts_on, ends_on, '[]') WITH &&);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coaching_periods");
        }
    }
}

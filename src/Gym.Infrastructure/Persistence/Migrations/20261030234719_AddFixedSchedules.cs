using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fixed_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_schedules", x => x.id);
                    table.CheckConstraint("ck_fixed_schedules_ends_on_not_before_starts_on", "ends_on >= starts_on");
                    table.ForeignKey(
                        name: "fk_fixed_schedules_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fixed_schedules_trainer_links_link_id",
                        column: x => x.link_id,
                        principalTable: "trainer_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fixed_schedules_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    booked_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_bookings", x => x.id);
                    table.CheckConstraint("ck_session_bookings_booked_by", "booked_by IN ('Trainer', 'Member')");
                    table.CheckConstraint("ck_session_bookings_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_session_bookings_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");
                    table.CheckConstraint("ck_session_bookings_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
                    table.ForeignKey(
                        name: "fk_session_bookings_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_bookings_trainer_links_link_id",
                        column: x => x.link_id,
                        principalTable: "trainer_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_bookings_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixed_schedule_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_schedule_slots", x => x.id);
                    table.CheckConstraint("ck_fixed_schedule_slots_day", "day BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_fixed_schedule_slots_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_fixed_schedule_slots_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");
                    table.CheckConstraint("ck_fixed_schedule_slots_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
                    table.ForeignKey(
                        name: "fk_fixed_schedule_slots_fixed_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "fixed_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_skips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    booked_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_skips", x => x.id);
                    table.CheckConstraint("ck_session_skips_booked_by", "booked_by IN ('Trainer', 'Member')");
                    table.CheckConstraint("ck_session_skips_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_session_skips_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");
                    table.CheckConstraint("ck_session_skips_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
                    table.ForeignKey(
                        name: "fk_session_skips_fixed_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "fixed_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fixed_schedule_slots_schedule_id_day_start_time_end_time",
                table: "fixed_schedule_slots",
                columns: new[] { "schedule_id", "day", "start_time", "end_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fixed_schedules_link_id",
                table: "fixed_schedules",
                column: "link_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixed_schedules_member_id_ends_on",
                table: "fixed_schedules",
                columns: new[] { "member_id", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_fixed_schedules_trainer_id_ends_on",
                table: "fixed_schedules",
                columns: new[] { "trainer_id", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_link_id",
                table: "session_bookings",
                column: "link_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_member_id_date_start_time_end_time",
                table: "session_bookings",
                columns: new[] { "member_id", "date", "start_time", "end_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_bookings_trainer_id_date",
                table: "session_bookings",
                columns: new[] { "trainer_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_session_skips_schedule_id_date_start_time_end_time",
                table: "session_skips",
                columns: new[] { "schedule_id", "date", "start_time", "end_time" },
                unique: true);

            // BUSINESS_RULES.md §15: one fixed schedule per student and trainer at a time. Per link,
            // not per member: a link that ended leaves its schedule's row as it was (the dates after
            // the link's last day are simply not read), and the member's next trainer starts a new
            // link. EF Core cannot declare an exclusion constraint; btree_gist is already installed.
            // Dropped with its table in Down.
            migrationBuilder.Sql(
                """
                ALTER TABLE fixed_schedules
                    ADD CONSTRAINT ex_fixed_schedules_link_id_dates
                    EXCLUDE USING gist (link_id WITH =, daterange(starts_on, ends_on, '[]') WITH &&);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixed_schedule_slots");

            migrationBuilder.DropTable(
                name: "session_bookings");

            migrationBuilder.DropTable(
                name: "session_skips");

            migrationBuilder.DropTable(
                name: "fixed_schedules");
        }
    }
}

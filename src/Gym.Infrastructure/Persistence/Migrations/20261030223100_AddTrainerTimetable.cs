using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerTimetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "timetable_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timetable_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_timetable_days_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "week_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_week_patterns", x => x.id);
                    table.ForeignKey(
                        name: "fk_week_patterns_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "timetable_day_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timetable_day_sessions", x => x.id);
                    table.CheckConstraint("ck_timetable_day_sessions_capacity", "capacity BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_timetable_day_sessions_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_timetable_day_sessions_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");
                    table.CheckConstraint("ck_timetable_day_sessions_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
                    table.ForeignKey(
                        name: "fk_timetable_day_sessions_timetable_days_day_id",
                        column: x => x.day_id,
                        principalTable: "timetable_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "week_pattern_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_week_pattern_sessions", x => x.id);
                    table.CheckConstraint("ck_week_pattern_sessions_capacity", "capacity BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_week_pattern_sessions_day", "day BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_week_pattern_sessions_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_week_pattern_sessions_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");
                    table.CheckConstraint("ck_week_pattern_sessions_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
                    table.ForeignKey(
                        name: "fk_week_pattern_sessions_week_patterns_pattern_id",
                        column: x => x.pattern_id,
                        principalTable: "week_patterns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_day_sessions_day_id_start_time_end_time",
                table: "timetable_day_sessions",
                columns: new[] { "day_id", "start_time", "end_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_timetable_days_trainer_id_date",
                table: "timetable_days",
                columns: new[] { "trainer_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_week_pattern_sessions_pattern_id_day_start_time_end_time",
                table: "week_pattern_sessions",
                columns: new[] { "pattern_id", "day", "start_time", "end_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_week_patterns_trainer_id_starts_on",
                table: "week_patterns",
                columns: new[] { "trainer_id", "starts_on" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "timetable_day_sessions");

            migrationBuilder.DropTable(
                name: "week_pattern_sessions");

            migrationBuilder.DropTable(
                name: "timetable_days");

            migrationBuilder.DropTable(
                name: "week_patterns");
        }
    }
}

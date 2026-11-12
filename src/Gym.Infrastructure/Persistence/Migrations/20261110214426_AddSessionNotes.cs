using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "week_pattern_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "timetable_day_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_notes", x => x.id);
                    table.CheckConstraint("ck_session_notes_end_after_start", "end_time > start_time");
                    table.CheckConstraint("ck_session_notes_end_time_on_step", "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");
                    table.CheckConstraint("ck_session_notes_start_time_on_step", "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");
                    table.CheckConstraint("ck_session_notes_text_not_blank", "text IS NULL OR (text <> '' AND text = btrim(text, E' \\t\\r\\n'))");
                    table.CheckConstraint("ck_session_notes_within_hours", "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");
                    table.ForeignKey(
                        name: "fk_session_notes_trainers_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "trainers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_note_not_blank",
                table: "week_pattern_sessions",
                sql: "note IS NULL OR (note <> '' AND note = btrim(note, E' \\t\\r\\n'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_note_not_blank",
                table: "timetable_day_sessions",
                sql: "note IS NULL OR (note <> '' AND note = btrim(note, E' \\t\\r\\n'))");

            migrationBuilder.CreateIndex(
                name: "ix_session_notes_trainer_id_date_start_time_end_time",
                table: "session_notes",
                columns: new[] { "trainer_id", "date", "start_time", "end_time" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_notes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_note_not_blank",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_note_not_blank",
                table: "timetable_day_sessions");

            migrationBuilder.DropColumn(
                name: "note",
                table: "week_pattern_sessions");

            migrationBuilder.DropColumn(
                name: "note",
                table: "timetable_day_sessions");
        }
    }
}

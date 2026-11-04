using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitSessionHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_end_time_on_step",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_start_time_on_step",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_end_time_on_step",
                table: "timetable_day_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_start_time_on_step",
                table: "timetable_day_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_end_time_on_step",
                table: "session_skips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_start_time_on_step",
                table: "session_skips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_cancellations_end_time_on_step",
                table: "session_cancellations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_cancellations_start_time_on_step",
                table: "session_cancellations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_bookings_end_time_on_step",
                table: "session_bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_bookings_start_time_on_step",
                table: "session_bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fixed_schedule_slots_end_time_on_step",
                table: "fixed_schedule_slots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fixed_schedule_slots_start_time_on_step",
                table: "fixed_schedule_slots");

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_end_time_on_step",
                table: "week_pattern_sessions",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_start_time_on_step",
                table: "week_pattern_sessions",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_within_hours",
                table: "week_pattern_sessions",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_end_time_on_step",
                table: "timetable_day_sessions",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_start_time_on_step",
                table: "timetable_day_sessions",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_within_hours",
                table: "timetable_day_sessions",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_end_time_on_step",
                table: "session_skips",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_start_time_on_step",
                table: "session_skips",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_within_hours",
                table: "session_skips",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_cancellations_end_time_on_step",
                table: "session_cancellations",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_cancellations_start_time_on_step",
                table: "session_cancellations",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_cancellations_within_hours",
                table: "session_cancellations",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_bookings_end_time_on_step",
                table: "session_bookings",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_bookings_start_time_on_step",
                table: "session_bookings",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_bookings_within_hours",
                table: "session_bookings",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fixed_schedule_slots_end_time_on_step",
                table: "fixed_schedule_slots",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fixed_schedule_slots_start_time_on_step",
                table: "fixed_schedule_slots",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fixed_schedule_slots_within_hours",
                table: "fixed_schedule_slots",
                sql: "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_end_time_on_step",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_start_time_on_step",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_week_pattern_sessions_within_hours",
                table: "week_pattern_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_end_time_on_step",
                table: "timetable_day_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_start_time_on_step",
                table: "timetable_day_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_timetable_day_sessions_within_hours",
                table: "timetable_day_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_end_time_on_step",
                table: "session_skips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_start_time_on_step",
                table: "session_skips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_within_hours",
                table: "session_skips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_cancellations_end_time_on_step",
                table: "session_cancellations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_cancellations_start_time_on_step",
                table: "session_cancellations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_cancellations_within_hours",
                table: "session_cancellations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_bookings_end_time_on_step",
                table: "session_bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_bookings_start_time_on_step",
                table: "session_bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_bookings_within_hours",
                table: "session_bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fixed_schedule_slots_end_time_on_step",
                table: "fixed_schedule_slots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fixed_schedule_slots_start_time_on_step",
                table: "fixed_schedule_slots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fixed_schedule_slots_within_hours",
                table: "fixed_schedule_slots");

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_end_time_on_step",
                table: "week_pattern_sessions",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_week_pattern_sessions_start_time_on_step",
                table: "week_pattern_sessions",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_end_time_on_step",
                table: "timetable_day_sessions",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_timetable_day_sessions_start_time_on_step",
                table: "timetable_day_sessions",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_end_time_on_step",
                table: "session_skips",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_start_time_on_step",
                table: "session_skips",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_cancellations_end_time_on_step",
                table: "session_cancellations",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_cancellations_start_time_on_step",
                table: "session_cancellations",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_bookings_end_time_on_step",
                table: "session_bookings",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_bookings_start_time_on_step",
                table: "session_bookings",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fixed_schedule_slots_end_time_on_step",
                table: "fixed_schedule_slots",
                sql: "extract(second FROM end_time) = 0 AND extract(minute FROM end_time)::int % 5 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fixed_schedule_slots_start_time_on_step",
                table: "fixed_schedule_slots",
                sql: "extract(second FROM start_time) = 0 AND extract(minute FROM start_time)::int % 5 = 0");
        }
    }
}

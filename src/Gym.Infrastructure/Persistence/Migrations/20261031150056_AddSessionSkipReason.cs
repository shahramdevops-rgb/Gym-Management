using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSkipReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "session_skips",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Rows already there are carried forward: a member's skip saved with a booking of theirs
            // (one save, so the same created_at) was a move, any other a leave; the trainer's took
            // the student out (passing dates over came with the reason).
            migrationBuilder.Sql(
                """
                UPDATE session_skips AS skip
                SET reason = CASE
                    WHEN skip.booked_by = 'Trainer' THEN 'TakenOut'
                    WHEN EXISTS (
                        SELECT 1
                        FROM fixed_schedules AS schedule
                        JOIN session_bookings AS booking
                          ON booking.member_id = schedule.member_id AND booking.booked_by = 'Member'
                        WHERE schedule.id = skip.schedule_id AND booking.created_at = skip.created_at) THEN 'Moved'
                    ELSE 'Left'
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_skips_reason",
                table: "session_skips",
                sql: "(reason IN ('Left', 'Moved') AND booked_by = 'Member') OR (reason IN ('TakenOut', 'PassedOver') AND booked_by = 'Trainer')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_session_skips_reason",
                table: "session_skips");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "session_skips");
        }
    }
}

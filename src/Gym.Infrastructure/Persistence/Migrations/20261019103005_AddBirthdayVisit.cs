using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdayVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_birthday_days",
                table: "sms_settings");

            migrationBuilder.AddColumn<short>(
                name: "birthday_year",
                table: "attendances",
                type: "smallint",
                nullable: true);

            // Carried forward, never refused (CLAUDE.md): the birthday SMS is never sent on the day
            // itself any more (task 10.8), so a 0 the Owner saved becomes the nearest allowed, 1.
            migrationBuilder.Sql("UPDATE sms_settings SET birthday_days_before = 1 WHERE birthday_days_before = 0;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_birthday_days",
                table: "sms_settings",
                sql: "birthday_days_before IS NULL OR birthday_days_before BETWEEN 1 AND 7");

            migrationBuilder.CreateIndex(
                name: "ix_attendances_one_birthday_visit_per_member_and_year",
                table: "attendances",
                columns: new[] { "member_id", "birthday_year" },
                unique: true,
                filter: "birthday_year IS NOT NULL AND cancelled_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendances_birthday_visit_on_plan",
                table: "attendances",
                sql: "birthday_year IS NULL OR (member_id IS NOT NULL AND subscription_id IS NOT NULL AND NOT is_cardio_only)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sms_settings_birthday_days",
                table: "sms_settings");

            migrationBuilder.DropIndex(
                name: "ix_attendances_one_birthday_visit_per_member_and_year",
                table: "attendances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendances_birthday_visit_on_plan",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "birthday_year",
                table: "attendances");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sms_settings_birthday_days",
                table: "sms_settings",
                sql: "birthday_days_before IS NULL OR birthday_days_before BETWEEN 0 AND 7");
        }
    }
}

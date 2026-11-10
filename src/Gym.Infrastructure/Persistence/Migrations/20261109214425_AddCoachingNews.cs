using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "answer_seen_at",
                table: "trainer_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "answered_on",
                table: "trainer_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "end_seen_at",
                table: "trainer_links",
                type: "timestamp with time zone",
                nullable: true);

            // Task 15.4d: the server's earlier answers and ends are old news, so each is marked seen
            // and nobody finds a card about them after the release. The answers keep no day: when
            // they were given was never kept.
            migrationBuilder.Sql(
                "UPDATE trainer_requests SET answer_seen_at = now() WHERE status IN ('Accepted', 'Declined');");
            migrationBuilder.Sql(
                "UPDATE trainer_links SET end_seen_at = now() WHERE ended_on IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trainer_requests_answer_seen_at_with_answer",
                table: "trainer_requests",
                sql: "answer_seen_at IS NULL OR status IN ('Accepted', 'Declined')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trainer_requests_answered_on_with_answer",
                table: "trainer_requests",
                sql: "answered_on IS NULL OR status IN ('Accepted', 'Declined')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trainer_links_end_seen_at_with_ended_on",
                table: "trainer_links",
                sql: "end_seen_at IS NULL OR ended_on IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_trainer_requests_answer_seen_at_with_answer",
                table: "trainer_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trainer_requests_answered_on_with_answer",
                table: "trainer_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trainer_links_end_seen_at_with_ended_on",
                table: "trainer_links");

            migrationBuilder.DropColumn(
                name: "answer_seen_at",
                table: "trainer_requests");

            migrationBuilder.DropColumn(
                name: "answered_on",
                table: "trainer_requests");

            migrationBuilder.DropColumn(
                name: "end_seen_at",
                table: "trainer_links");
        }
    }
}

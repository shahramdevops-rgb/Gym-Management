using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The single-session plan and the sales made from it (BUSINESS_RULES.md §3, §4, task 6.5.3).
    /// </summary>
    /// <remarks>
    /// The important part is hand-written: the no-overlap exclusion constraint is recreated with
    /// <c>AND NOT is_single_session</c> in its condition, so a single visit may share a date with a
    /// membership while two memberships still may not. EF Core cannot express an exclusion
    /// constraint, so it never appears in the model and a scaffolded migration leaves it alone.
    /// </remarks>
    /// <inheritdoc />
    public partial class AddSingleSessionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_single_session",
                table: "subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Every plan that existed before this one is a membership. Scaffolding gave this an
            // empty string, which is not a PlanKind and would come back as a conversion error the
            // first time an old plan was read.
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Membership");

            // Both defaults stay. They are needed once, to fill the rows already in the table, but
            // they also keep every hand-written INSERT that predates these columns working, and
            // "membership, not a single visit" is the right answer for a row that does not say.
            migrationBuilder.AddCheckConstraint(
                name: "ck_subscriptions_single_session_shape",
                table: "subscriptions",
                sql: "NOT is_single_session OR (duration_days = 1 AND total_sessions = 1)");

            migrationBuilder.CreateIndex(
                name: "ux_plans_single_session",
                table: "plans",
                column: "kind",
                unique: true,
                filter: "kind = 'SingleSession'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plans_single_session_shape",
                table: "plans",
                sql: "kind <> 'SingleSession' OR (duration_days = 1 AND session_count = 1)");

            // BUSINESS_RULES.md §4: a single visit is invisible to the no-overlap rule, in both
            // directions — it may cover a date a membership covers, and two of them may cover the
            // same date, which is how a member comes twice in one day. What stays guarded is the
            // rule that matters: two memberships never cover the same date. Everything else about
            // the constraint is unchanged, DEFERRABLE included (see AddSubscriptions for why).
            migrationBuilder.Sql(
                """
                ALTER TABLE subscriptions DROP CONSTRAINT ex_subscriptions_no_overlap;

                ALTER TABLE subscriptions
                    ADD CONSTRAINT ex_subscriptions_no_overlap
                    EXCLUDE USING gist (member_id WITH =, daterange(start_date, end_date, '[]') WITH &&)
                    WHERE (cancelled_at IS NULL AND NOT is_single_session)
                    DEFERRABLE INITIALLY IMMEDIATE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old constraint goes back before the column it no longer mentions is dropped. If
            // any single visit overlaps a membership by then, this fails — correctly: those rows
            // are exactly what the old rule forbade, and silently deleting them is not a rollback.
            migrationBuilder.Sql(
                """
                ALTER TABLE subscriptions DROP CONSTRAINT ex_subscriptions_no_overlap;

                ALTER TABLE subscriptions
                    ADD CONSTRAINT ex_subscriptions_no_overlap
                    EXCLUDE USING gist (member_id WITH =, daterange(start_date, end_date, '[]') WITH &&)
                    WHERE (cancelled_at IS NULL)
                    DEFERRABLE INITIALLY IMMEDIATE;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscriptions_single_session_shape",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "ux_plans_single_session",
                table: "plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plans_single_session_shape",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "is_single_session",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "plans");
        }
    }
}

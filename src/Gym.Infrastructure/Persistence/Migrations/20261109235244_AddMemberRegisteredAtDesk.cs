using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Task 16.1, BUSINESS_RULES.md §16 <i>Site users</i>: where each member came from. Only adds, and
    /// carries every existing row forward (the server holds real data).
    /// </summary>
    /// <remarks>
    /// Every existing member starts as registered at the desk; then those who signed up on the
    /// site are marked. They are told apart by two things together: no staff user created the row
    /// (a site sign-up is an anonymous request, so <c>created_by</c> is null, while every desk
    /// registration carries the staff user's id), and the member has a site account. Whether they
    /// are still site users (no plan, no visit) is worked out when asked, not stored, so the ones
    /// who already bought a plan need nothing here. The default only fills the existing rows and is
    /// dropped afterwards: the application always writes the value itself.
    /// </remarks>
    public partial class AddMemberRegisteredAtDesk : Migration
    {
        /// <summary>Marks the existing site sign-ups (MemberRegisteredAtDeskMigrationTests runs it on rows of each kind).</summary>
        private const string MarkSiteSignUpsSql =
            """
            UPDATE members AS m
            SET registered_at_desk = false
            WHERE m.created_by IS NULL
              AND EXISTS (SELECT 1 FROM site_accounts AS a WHERE a.member_id = m.id);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "registered_at_desk",
                table: "members",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(MarkSiteSignUpsSql);

            migrationBuilder.Sql("ALTER TABLE members ALTER COLUMN registered_at_desk DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "registered_at_desk",
                table: "members");
        }
    }
}

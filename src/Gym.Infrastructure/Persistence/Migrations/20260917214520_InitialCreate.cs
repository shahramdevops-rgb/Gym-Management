using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deliberately empty. Task 0.3 builds the persistence plumbing, and the first entities
    /// arrive with Identity in task 1.1, so there is nothing to create yet.
    /// </summary>
    /// <remarks>
    /// Applying it still does two useful things: it creates the __EFMigrationsHistory table,
    /// and it fixes AppDbContextModelSnapshot as the baseline every later migration is
    /// diffed against, so task 1.1 produces a clean delta instead of one enormous first
    /// migration.
    /// </remarks>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

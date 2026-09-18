using Gym.Api.IntegrationTests.Infrastructure;

using Npgsql;

namespace Gym.Api.IntegrationTests.Identity;

/// <summary>
/// The <c>users</c> table's invariants, proved with raw SQL rather than through
/// <c>UserManager</c>. <c>UserManager</c> checks for a duplicate user name before it inserts,
/// so going through it would only prove the C# check; these prove the database refuses the
/// row even when no application code is in the way.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class UserTableConstraintTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    [Fact]
    public async Task Users_DuplicateNormalizedUserName_RejectedByDatabase()
    {
        await using var connection = await OpenAsync();

        await InsertUserAsync(connection, normalizedUserName: "OWNER", fullName: "مدیر");

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertUserAsync(connection, normalizedUserName: "OWNER", fullName: "مدیر دوم"));

        exception.SqlState.ShouldBe(UniqueViolation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Users_BlankFullName_RejectedByCheckConstraint(string fullName)
    {
        await using var connection = await OpenAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertUserAsync(connection, normalizedUserName: "STAFF", fullName: fullName));

        exception.SqlState.ShouldBe(CheckViolation);
        exception.ConstraintName.ShouldBe("ck_users_full_name_not_blank");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static async Task InsertUserAsync(NpgsqlConnection connection, string normalizedUserName, string fullName)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO users (id, full_name, is_active, must_change_password, user_name, normalized_user_name,
                               email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled,
                               access_failed_count)
            VALUES (@id, @fullName, true, true, @userName, @normalizedUserName, false, false, false, true, 0)
            """,
            connection);

        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("fullName", fullName);
        command.Parameters.AddWithValue("userName", normalizedUserName.ToLowerInvariant());
        command.Parameters.AddWithValue("normalizedUserName", normalizedUserName);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

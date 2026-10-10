using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;

using Npgsql;

namespace Gym.Api.IntegrationTests.Payments;

/// <summary>
/// A cafe payment points at an order that exists, checked by the database and not only by the
/// code (CLAUDE.md, task 11.3). Inserted with SQL, past every handler, as a bug or a hand-typed
/// query would.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PaymentConstraintTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Payments_UnknownCafeOrderInsertedDirectly_RejectedByTheForeignKey()
    {
        var user = await TestUsers.CreateAsync(Fixture);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO payments (id, cafe_order_id, kind, amount, method, paid_at, received_by_user_id, created_at)
            VALUES (@id, @order, 'Payment', 30000, 'Cash', now(), @user, now())
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("order", Guid.CreateVersion7());
        command.Parameters.AddWithValue("user", user.Id);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        exception.SqlState.ShouldBe("23503");
        exception.ConstraintName.ShouldBe("fk_payments_cafe_orders_cafe_order_id");
    }
}

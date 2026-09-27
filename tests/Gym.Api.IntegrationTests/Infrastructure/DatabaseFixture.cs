using Gym.Domain.Expenses;
using Gym.Domain.Pricing;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Respawn;
using Respawn.Graph;

using Testcontainers.PostgreSql;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real Postgres for the database-backed tests, started on demand and thrown away after.
/// </summary>
/// <remarks>
/// <para>
/// The container runs the same <c>postgres:18</c> image as production, which is the whole
/// point: CLAUDE.md bans the EF Core InMemory provider because it is a <i>different</i>
/// provider that happily accepts what Postgres rejects — unique indexes, check constraints,
/// concurrency tokens — so a test passing there proves nothing about the real database.
/// </para>
/// <para>
/// It is also emphatically not the <c>gym-postgres</c> container from docker-compose.yml.
/// Testcontainers starts its own on a random port and removes it afterwards, so running the
/// test suite can never truncate the data you are looking at in development.
/// </para>
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("gym_tests")
        .WithUsername("gym")
        .WithPassword("gym-tests")
        .Build();

    private GymApiFactory? _factory;
    private NpgsqlConnection? _connection;
    private Respawner? _respawner;

    /// <summary>
    /// How the database is emptied between tests.
    /// </summary>
    /// <remarks>
    /// Exposed so a test can assert on the real configuration rather than on a copy of it.
    /// <c>__EFMigrationsHistory</c> has to be ignored: Respawn deletes rows, not tables, so
    /// clearing that one would leave a fully migrated database that believes it has never been
    /// migrated, and the next run would try to apply every migration on top of itself.
    /// </remarks>
    public static RespawnerOptions RespawnOptions => new()
    {
        DbAdapter = DbAdapter.Postgres,
        SchemasToInclude = ["public"],
        TablesToIgnore = [new Table("__EFMigrationsHistory")],
    };

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);

        _factory = new GymApiFactory(ConnectionString);

        // The schema is built by the migrations themselves, never by EnsureCreated: a test
        // database created some other way would not prove that the migrations actually apply.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        // One long-lived connection for the resets. Respawn is not created here: see ResetAsync.
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        ExpenseCategoriesAfterMigration = await ReadExpenseCategoriesAsync(_connection);
        LockersAfterMigration = await ReadLockersAsync(_connection);
        PriceListsAfterMigration = await ReadPriceListsAsync(_connection);
    }

    /// <summary>
    /// An <see cref="HttpClient"/> that talks to the in-process server. It does not keep
    /// cookies: the refresh cookie is <c>Secure</c> and the test server speaks plain HTTP, so a
    /// cookie jar would silently drop it. Tests read <c>Set-Cookie</c> and send <c>Cookie</c>
    /// themselves, which also makes the cookie under test visible in the test.
    /// </summary>
    public HttpClient CreateClient() => Factory.CreateClient(ClientOptions);

    /// <summary>
    /// A client for a separate host with extra settings, for the rare test that needs different
    /// configuration (a low rate limit, say). It shares the database but not the services, so
    /// its singletons, such as the rate limiter's counters, start fresh.
    /// </summary>
    public HttpClient CreateClient(IDictionary<string, string?> settings, Action<IWebHostBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Factory
            .WithWebHostBuilder(builder =>
            {
                foreach (var (key, value) in settings)
                {
                    builder.UseSetting(key, value);
                }

                configure?.Invoke(builder);
            })
            .CreateClient(ClientOptions);
    }

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = false };

    /// <summary>A scope for resolving scoped services such as <see cref="AppDbContext"/>.</summary>
    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();

    /// <summary>The app's root services, for code that makes its own scopes (the server console).</summary>
    public IServiceProvider Services => Factory.Services;

    /// <summary>
    /// Empties every table before a test runs. Cheaper than a fresh container or a fresh set
    /// of migrations, which is what makes per-test isolation affordable at all.
    /// </summary>
    /// <remarks>
    /// The Respawner is built on first use rather than at start-up. It inspects the schema and
    /// refuses to build a delete plan for a database that has no resettable tables — and until
    /// task 1.1 adds the first entity, the only table here is the ignored migration history.
    /// So the check is explicit: with nothing to reset, this is a no-op, and the moment a real
    /// table exists the plan is built and cached.
    /// </remarks>
    public async Task ResetAsync()
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("The database fixture has not been initialized.");
        }

        _respawner ??= await HasResettableTablesAsync(_connection)
            ? await Respawner.CreateAsync(_connection, RespawnOptions)
            : null;

        if (_respawner is not null)
        {
            await _respawner.ResetAsync(_connection);
            await RestoreSeedDataAsync(_connection);
        }
    }

    /// <summary>
    /// The expense categories a fresh database gets from its migration, as they were read right
    /// after migrating and before any reset. It is the one moment the migration's own rows are
    /// visible, so it is what proves the migration seeds them.
    /// </summary>
    public IReadOnlyList<(Guid Id, string Name, string NormalizedName)> ExpenseCategoriesAfterMigration { get; private set; } = [];

    /// <summary>The lockers a fresh database gets from its migration, read at the same moment as <see cref="ExpenseCategoriesAfterMigration"/>.</summary>
    public IReadOnlyList<(Guid Id, int Number, bool IsOutOfService)> LockersAfterMigration { get; private set; } = [];

    /// <summary>The price list rows a fresh database gets from its migration: one, with both prices empty.</summary>
    public IReadOnlyList<(Guid Id, decimal? SessionPrice, decimal? SingleVisitPrice)> PriceListsAfterMigration { get; private set; } = [];

    /// <summary>
    /// Puts back the rows the migrations seed, which Respawn deleted with everything else. A
    /// test then starts from what a freshly migrated database holds, not from an emptier one that
    /// production never sees. The rows come from <see cref="ExpenseCategorySeed"/>,
    /// <see cref="LockerSeed"/> and <see cref="PriceList.TheId"/>, the same values the migrations
    /// were generated from.
    /// </summary>
    private static async Task RestoreSeedDataAsync(NpgsqlConnection connection)
    {
        await RestoreExpenseCategoriesAsync(connection);
        await RestoreLockersAsync(connection);
        await RestorePriceListAsync(connection);
    }

    /// <summary>The one price list, with both prices empty, as the migration seeds it (BUSINESS_RULES.md §3).</summary>
    private static async Task RestorePriceListAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO price_lists (id, created_at) VALUES (@id, now())", connection);

        command.Parameters.AddWithValue("id", PriceList.TheId);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<(Guid Id, decimal? SessionPrice, decimal? SingleVisitPrice)>> ReadPriceListsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT id, session_price, single_visit_price FROM price_lists", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var rows = new List<(Guid, decimal?, decimal?)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                reader.IsDBNull(2) ? null : reader.GetDecimal(2)));
        }

        return rows;
    }

    private static async Task RestoreLockersAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO lockers (id, number, is_out_of_service, created_at)
            SELECT seed.id, seed.number, false, @created_at
            FROM unnest(@ids, @numbers) AS seed(id, number)
            """,
            connection);

        command.Parameters.AddWithValue("created_at", LockerSeed.CreatedAt);
        command.Parameters.AddWithValue("ids", LockerSeed.All.Select(seed => seed.Id).ToArray());
        command.Parameters.AddWithValue("numbers", LockerSeed.All.Select(seed => seed.Number).ToArray());

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<(Guid Id, int Number, bool IsOutOfService)>> ReadLockersAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT id, number, is_out_of_service FROM lockers ORDER BY number", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var lockers = new List<(Guid, int, bool)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            lockers.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetBoolean(2)));
        }

        return lockers;
    }

    private static async Task RestoreExpenseCategoriesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO expense_categories (id, name, normalized_name, created_at)
            SELECT seed.id, seed.name, seed.normalized_name, @created_at
            FROM unnest(@ids, @names, @normalized_names) AS seed(id, name, normalized_name)
            """,
            connection);

        command.Parameters.AddWithValue("created_at", ExpenseCategorySeed.CreatedAt);
        command.Parameters.AddWithValue("ids", ExpenseCategorySeed.All.Select(seed => seed.Id).ToArray());
        command.Parameters.AddWithValue("names", ExpenseCategorySeed.All.Select(seed => seed.Name).ToArray());
        command.Parameters.AddWithValue(
            "normalized_names", ExpenseCategorySeed.All.Select(seed => ExpenseCategory.Normalize(seed.Name)).ToArray());

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<(Guid Id, string Name, string NormalizedName)>> ReadExpenseCategoriesAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT id, name, normalized_name FROM expense_categories ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var categories = new List<(Guid, string, string)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            categories.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return categories;
    }

    /// <summary>Whether the schema holds anything Respawn would be able to empty.</summary>
    private static async Task<bool> HasResettableTablesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory'
            """,
            connection);

        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        // Stops and removes the container, so a test run leaves nothing behind on the machine.
        await _container.DisposeAsync();
    }

    private GymApiFactory Factory =>
        _factory ?? throw new InvalidOperationException("The database fixture has not been initialized.");
}

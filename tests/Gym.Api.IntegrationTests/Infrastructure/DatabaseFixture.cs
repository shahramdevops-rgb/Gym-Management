using Gym.Infrastructure.Persistence;

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
    public HttpClient CreateClient(IDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Factory
            .WithWebHostBuilder(builder =>
            {
                foreach (var (key, value) in settings)
                {
                    builder.UseSetting(key, value);
                }
            })
            .CreateClient(ClientOptions);
    }

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = false };

    /// <summary>A scope for resolving scoped services such as <see cref="AppDbContext"/>.</summary>
    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();

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
        }
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

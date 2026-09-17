using Gym.Domain.Common;
using Gym.Infrastructure.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;

namespace Gym.Api.IntegrationTests.Persistence;

/// <summary>
/// A stand-in entity. The real model has no entities until task 1.1 seeds Identity, so the
/// persistence conventions are asserted against this one instead.
/// </summary>
public sealed class TestEntity : Entity
{
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// A context configured exactly like <c>AppDbContext</c> — same provider, same naming
/// convention, same interceptor — but with a model of its own.
/// </summary>
/// <remarks>
/// Nothing here connects to a database. EF Core builds the model and tracks changes entirely
/// in memory; a connection is only opened when a query or <c>SaveChanges</c> actually runs.
/// That is what lets these tests assert on column naming and on the interceptor's behaviour
/// without Docker, and it is not the banned InMemory provider: the Npgsql provider is the one
/// building this model, so its type mappings are the real ones. Database-backed tests arrive
/// with the Testcontainers harness in task 0.6.
/// </remarks>
public sealed class PersistenceTestContext(DbContextOptions<PersistenceTestContext> options)
    : DbContext(options)
{
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    public static PersistenceTestContext Create(AuditableEntityInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<PersistenceTestContext>()
            // A syntactically valid connection string that is never dialled.
            .UseNpgsql("Host=localhost;Database=gym_tests;Username=gym;Password=unused")
            .UseSnakeCaseNamingConvention();

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new PersistenceTestContext(builder.Options);
    }
}

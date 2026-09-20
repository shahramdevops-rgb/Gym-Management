using Gym.Application.Common;
using Gym.Application.Subscriptions;
using Gym.Domain.Attendances;
using Gym.Domain.Audit;
using Gym.Domain.Auth;
using Gym.Domain.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

namespace Gym.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the whole modular monolith. It implements
/// <see cref="IAppDbContext"/> so use cases can depend on the interface instead of on this
/// class, and it is registered together with the audit interceptor in
/// <c>DependencyInjection.AddInfrastructure</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Not on <c>IAppDbContext</c> yet: nothing in Application reads the audit log until the
    /// audit screen (task 11.1), and only the interceptor writes it.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Locker> Lockers => Set<Locker>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// <c>SELECT ... FOR UPDATE</c> on the member's row: held until the transaction ends, so a
    /// second transaction asking for the same lock waits instead of reading a stale calendar.
    /// </summary>
    public Task LockMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A row lock outside a transaction is released at once; begin one first.");
        }

        return Database.ExecuteSqlAsync($"SELECT 1 FROM members WHERE id = {memberId} FOR UPDATE", cancellationToken);
    }

    /// <inheritdoc cref="IAppDbContext.DeferSubscriptionOverlapCheckAsync"/>
    public Task DeferSubscriptionOverlapCheckAsync(CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Deferring a constraint outside a transaction has no effect; begin one first.");
        }

        // The constraint name cannot be a parameter: Postgres does not allow binding identifiers.
        // SubscriptionConstraints.NoOverlap is a compile-time constant, not user input.
        return Database.ExecuteSqlRawAsync($"SET CONSTRAINTS {SubscriptionConstraints.NoOverlap} DEFERRED", cancellationToken);
    }

    /// <inheritdoc cref="IAppDbContext.CommitTransactionAsync"/>
    public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            throw new ExclusionConstraintException(exception.ConstraintName ?? string.Empty, exception);
        }
    }

    /// <summary>
    /// Translates a Postgres unique violation (SQLSTATE 23505) into Application's
    /// <see cref="UniqueConstraintException"/>, and an exclusion violation (23P01) into
    /// <see cref="ExclusionConstraintException"/>, naming the constraint, so handlers can answer
    /// the expected race with a 409 without knowing which database is behind them.
    /// </summary>
    /// <remarks>
    /// Every async save goes through this overload, including the parameterless one and
    /// Identity's stores. The synchronous path is not used by this application.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception is not UniqueConstraintException &&
                  exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            throw new UniqueConstraintException(unique.ConstraintName ?? string.Empty, exception);
        }
        catch (DbUpdateException exception)
            when (exception is not ExclusionConstraintException &&
                  exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } exclusion)
        {
            throw new ExclusionConstraintException(exclusion.ConstraintName ?? string.Empty, exclusion);
        }
        catch (PostgresException exclusion) when (exclusion.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            // A deferred constraint is checked at COMMIT, and the commit's error is not wrapped
            // in a DbUpdateException. A handler that commits its own transaction (task 4.3)
            // meets it at CommitAsync instead and must translate it there.
            throw new ExclusionConstraintException(exclusion.ConstraintName ?? string.Empty, exclusion);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's base OnModelCreating registers Users/Roles/UserRoles/etc. and names
        // their tables AspNetUsers, AspNetRoles, and so on. It has to run before
        // ApplyConfigurationsFromAssembly below, or UserConfiguration's ToTable("users")
        // would apply first and then be silently overwritten back to "AspNetUsers".
        base.OnModelCreating(builder);

        // Trigram indexes for "contains" searches (MemberConfiguration). A standard Postgres
        // extension, not a NuGet package; the migration runs CREATE EXTENSION IF NOT EXISTS.
        builder.HasPostgresExtension("pg_trgm");

        // GiST indexes on plain columns such as member_id, needed by the exclusion constraint
        // that keeps a member's subscriptions from overlapping (migration AddSubscriptions).
        // Ships with Postgres, like pg_trgm.
        builder.HasPostgresExtension("btree_gist");

        // Picks up every IEntityTypeConfiguration<T> in Gym.Infrastructure, so adding an
        // entity is "add one configuration file" and never "also remember to register it".
        builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        RenameIdentityTables(builder);
    }

    /// <summary>
    /// Renames the Identity tables that have no dedicated <see cref="IEntityTypeConfiguration{T}"/>
    /// of their own, from Identity's default "AspNetXxx" to snake_case, matching every other
    /// table in this schema.
    /// </summary>
    private static void RenameIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}

using Gym.Application.Common;
using Gym.Domain.Audit;
using Gym.Domain.Auth;
using Gym.Domain.Members;
using Gym.Domain.Plans;
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

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// Translates a Postgres unique violation (SQLSTATE 23505) into Application's
    /// <see cref="UniqueConstraintException"/>, naming the index, so handlers can answer the
    /// expected race with a 409 without knowing which database is behind them.
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

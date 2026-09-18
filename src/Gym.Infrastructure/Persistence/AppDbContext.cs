using Gym.Application.Common;
using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's base OnModelCreating registers Users/Roles/UserRoles/etc. and names
        // their tables AspNetUsers, AspNetRoles, and so on. It has to run before
        // ApplyConfigurationsFromAssembly below, or UserConfiguration's ToTable("users")
        // would apply first and then be silently overwritten back to "AspNetUsers".
        base.OnModelCreating(builder);

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

using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the whole modular monolith. It implements
/// <see cref="IAppDbContext"/> so use cases can depend on the interface instead of on this
/// class, and it is registered together with the audit interceptor in
/// <c>DependencyInjection.AddInfrastructure</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in Gym.Infrastructure, so adding an
        // entity is "add one configuration file" and never "also remember to register it".
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

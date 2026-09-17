using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Common;

/// <summary>
/// The Application layer's view of the database. Handlers depend on this, never on
/// <c>AppDbContext</c>, so the dependency arrow keeps pointing inward: Application declares
/// the contract and Infrastructure implements it.
/// </summary>
/// <remarks>
/// <para>
/// There are deliberately no repositories. A generic <c>IRepository&lt;T&gt;</c> would wrap a
/// component that is already a repository and a unit of work, and would have to re-expose
/// LINQ to stay useful. The price of skipping it is this interface: Application references
/// the EF Core abstractions (<see cref="DbSet{TEntity}"/>, <see cref="SaveChangesAsync"/>)
/// but no provider, so it still knows nothing about Postgres.
/// </para>
/// <para>
/// A <see cref="DbSet{TEntity}"/> property is added here by the task that introduces the
/// entity, which is why this interface is empty apart from saving today.
/// </para>
/// </remarks>
public interface IAppDbContext
{
    /// <summary>
    /// Commits the tracked changes as one transaction. The audit interceptor runs first, so
    /// callers must not set <c>CreatedAt</c> or <c>UpdatedAt</c> themselves.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

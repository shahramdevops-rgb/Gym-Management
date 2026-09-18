using Gym.Domain.Auth;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

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
/// entity. The Identity tables are not here: Application reaches users through
/// <c>IUserAuthenticator</c> instead (ADR 0002).
/// </para>
/// </remarks>
public interface IAppDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Member> Members { get; }

    /// <summary>
    /// Exposed for the rare handler that must recover from a failed save in the same request:
    /// after a <see cref="DbUpdateConcurrencyException"/> the tracked entities hold stale values,
    /// and <c>ChangeTracker.Clear()</c> discards them so fresh rows can be loaded.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// For a use case whose writes must all happen or none: changing a password saves through
    /// Identity's <c>UserManager</c> and then revokes refresh tokens here. Both use this same
    /// scoped context, so one transaction covers both saves.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Commits the tracked changes as one transaction. The audit interceptor runs first, so
    /// callers must not set <c>CreatedAt</c> or <c>UpdatedAt</c> themselves.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

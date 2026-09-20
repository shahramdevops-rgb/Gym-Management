using Gym.Domain.Auth;
using Gym.Domain.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

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

    DbSet<Plan> Plans { get; }

    DbSet<Subscription> Subscriptions { get; }

    DbSet<Locker> Lockers { get; }

    /// <summary>
    /// Exposed for the rare handler that must recover from a failed save in the same request:
    /// after a <see cref="DbUpdateConcurrencyException"/> the tracked entities hold stale values,
    /// and <c>ChangeTracker.Clear()</c> discards them so fresh rows can be loaded.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Locks one member's row until the current transaction ends, so use cases that change a
    /// member's subscriptions run one at a time for that member. The second waits, then reads
    /// what the first saved. Must be called inside <see cref="BeginTransactionAsync"/>.
    /// </summary>
    Task LockMemberAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// For a use case whose writes must all happen or none: changing a password saves through
    /// Identity's <c>UserManager</c> and then revokes refresh tokens here. Both use this same
    /// scoped context, so one transaction covers both saves.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Delays the subscriptions no-overlap exclusion check until <see cref="CommitTransactionAsync"/>
    /// instead of after each write, for a transaction that moves several of one member's
    /// subscriptions at once and must pass through a moment where their date ranges overlap
    /// (task 4.3 unfreeze). Must be called inside <see cref="BeginTransactionAsync"/>.
    /// </summary>
    Task DeferSubscriptionOverlapCheckAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Commits <paramref name="transaction"/>, translating a deferred exclusion-constraint
    /// violation into <see cref="ExclusionConstraintException"/> the same way
    /// <see cref="SaveChangesAsync"/> does for an immediate one. A deferred constraint is
    /// checked at commit, not at the write, so a caller that deferred it with
    /// <see cref="DeferSubscriptionOverlapCheckAsync"/> must commit through here to see that
    /// translation.
    /// </summary>
    Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the tracked changes as one transaction. The audit interceptor runs first, so
    /// callers must not set <c>CreatedAt</c> or <c>UpdatedAt</c> themselves.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

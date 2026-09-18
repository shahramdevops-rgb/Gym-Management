using Gym.Application.Common;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gym.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills the audit fields of every <see cref="Entity"/> on every save: when, and by whom.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that no handler ever stamps a timestamp. Forty use cases each remembering
/// to set <c>UpdatedAt</c> is forty chances to forget; one interceptor underneath
/// <c>SaveChanges</c> is zero.
/// </para>
/// <para>
/// The values are written through <c>entry.Property(...)</c> rather than through the entity's
/// own setters. EF Core's change tracker writes private setters directly, so the audit fields
/// stay encapsulated on <see cref="Entity"/> instead of being made public for this class's
/// benefit.
/// </para>
/// <para>
/// "By whom" comes from <see cref="ICurrentUser"/>. It is null for rows written by anonymous
/// requests (login), seeding and background jobs, which act on nobody's behalf.
/// <see cref="ICurrentUser"/> reads the current request through <c>IHttpContextAccessor</c>,
/// so this interceptor can stay a singleton and still see each request's own user.
/// </para>
/// </remarks>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider, ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        // Null when the save is not tied to a context, for example during some pooling paths.
        if (context is null)
        {
            return;
        }

        // One timestamp for the whole save, so every row touched by the same transaction
        // carries the same instant. GetUtcNow() returns a zero offset, which is what Npgsql
        // requires for timestamptz.
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Added rows get CreatedAt only: an insert is not an update, so a row
                    // that has never changed must keep a null UpdatedAt.
                    entry.Property(entity => entity.CreatedAt).CurrentValue = now;
                    entry.Property(entity => entity.CreatedBy).CurrentValue = userId;
                    break;

                case EntityState.Modified:
                    // CreatedAt is deliberately untouched here. Re-stamping it on every save
                    // is the classic version of this bug, and it silently destroys history.
                    entry.Property(entity => entity.UpdatedAt).CurrentValue = now;
                    entry.Property(entity => entity.UpdatedBy).CurrentValue = userId;
                    break;

                default:
                    // Unchanged, Deleted and Detached entries need no stamp. Deleted rows are
                    // disappearing, and the audit log in task 1.6 records the deletion itself.
                    break;
            }
        }
    }
}

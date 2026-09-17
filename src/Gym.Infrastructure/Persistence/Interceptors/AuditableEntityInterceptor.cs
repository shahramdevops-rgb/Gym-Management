using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gym.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills <see cref="Entity.CreatedAt"/> and <see cref="Entity.UpdatedAt"/> on every save.
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
/// <c>CreatedBy</c> and <c>UpdatedBy</c> are left null until task 1.4 introduces
/// <c>ICurrentUser</c>. They stay null for rows written by seeding and background jobs,
/// which act on nobody's behalf.
/// </para>
/// </remarks>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
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

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Added rows get CreatedAt only: an insert is not an update, so a row
                    // that has never changed must keep a null UpdatedAt.
                    entry.Property(entity => entity.CreatedAt).CurrentValue = now;
                    break;

                case EntityState.Modified:
                    // CreatedAt is deliberately untouched here. Re-stamping it on every save
                    // is the classic version of this bug, and it silently destroys history.
                    entry.Property(entity => entity.UpdatedAt).CurrentValue = now;
                    break;

                default:
                    // Unchanged, Deleted and Detached entries need no stamp. Deleted rows are
                    // disappearing, and the audit log in task 1.6 records the deletion itself.
                    break;
            }
        }
    }
}

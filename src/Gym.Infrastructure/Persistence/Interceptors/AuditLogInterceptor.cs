using System.Text.Json;
using System.Text.Json.Serialization;

using Gym.Application.Common;
using Gym.Domain.Audit;
using Gym.Domain.Common;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gym.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Adds an <see cref="AuditLog"/> row for every inserted, updated and deleted entity, inside the
/// same save (BUSINESS_RULES.md §11).
/// </summary>
/// <remarks>
/// <para>
/// Same save, so the same transaction: if the change is rolled back, so is its audit row, and
/// the log can never describe something that did not happen, or miss something that did.
/// </para>
/// <para>
/// Only changed properties are recorded, and some are never recorded at all: see
/// <see cref="ExcludedProperties"/>. An update whose only changes are excluded (a new security
/// stamp, say) writes no row, because a row that says "something changed" and shows nothing is
/// noise in the one table the Owner reads to find out what happened.
/// </para>
/// </remarks>
public sealed class AuditLogInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    /// <summary>
    /// Never written to the audit log, by property name, for every entity. Secrets first
    /// (BUSINESS_RULES.md §11); then values that change on every save and would only bury the
    /// real changes; then the audit fields the log row already records as who and when.
    /// </summary>
    /// <remarks>
    /// By name rather than per entity, so a future entity with a <c>TokenHash</c> is protected
    /// without anyone remembering to add it here.
    /// </remarks>
    public static readonly IReadOnlySet<string> ExcludedProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        // Secrets.
        nameof(IdentityUser<Guid>.PasswordHash),
        nameof(IdentityUser<Guid>.SecurityStamp),
        "TokenHash",

        // Change on every save.
        nameof(IdentityUser<Guid>.ConcurrencyStamp),
        "Version",

        // Recorded by the audit row itself.
        nameof(Entity.CreatedAt),
        nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedAt),
        nameof(Entity.UpdatedBy),
    };

    /// <summary>
    /// Identity's token table stores reset and authenticator tokens in plain text. Nothing uses
    /// it today, and if something ever does, it must not leak through the audit log.
    /// </summary>
    private static readonly Type ExcludedEntity = typeof(IdentityUserToken<Guid>);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        AddAuditRows(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        AddAuditRows(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // DetectChanges first, so entities modified without the change tracker noticing yet
        // are seen; ToList, because adding audit rows below changes the tracker's contents.
        context.ChangeTracker.DetectChanges();

        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is not AuditLog && entry.Metadata.ClrType != ExcludedEntity)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;
        var ipAddress = currentUser.IpAddress;

        foreach (var entry in entries)
        {
            var row = entry.State switch
            {
                EntityState.Added => Record(entry, AuditAction.Insert, oldValues: null, NewValues(entry, onlyChanged: false)),
                EntityState.Deleted => Record(entry, AuditAction.Delete, OldValues(entry, onlyChanged: false), newValues: null),
                _ => RecordUpdate(entry),
            };

            if (row is not null)
            {
                context.Add(row);
            }
        }

        AuditLog? RecordUpdate(EntityEntry entry)
        {
            var oldValues = OldValues(entry, onlyChanged: true);

            return oldValues.Count == 0
                ? null
                : Record(entry, AuditAction.Update, oldValues, NewValues(entry, onlyChanged: true));
        }

        AuditLog Record(
            EntityEntry entry,
            AuditAction action,
            Dictionary<string, object?>? oldValues,
            Dictionary<string, object?>? newValues) =>
            AuditLog.Record(
                action,
                entry.Metadata.DisplayName(),
                EntityId(entry),
                Serialize(oldValues),
                Serialize(newValues),
                userId,
                ipAddress,
                now);
    }

    private static Dictionary<string, object?> OldValues(EntityEntry entry, bool onlyChanged) =>
        AuditedProperties(entry, onlyChanged).ToDictionary(property => property.Metadata.Name, property => property.OriginalValue);

    private static Dictionary<string, object?> NewValues(EntityEntry entry, bool onlyChanged) =>
        AuditedProperties(entry, onlyChanged).ToDictionary(property => property.Metadata.Name, property => property.CurrentValue);

    /// <summary>
    /// For an update, a property counts as changed only if its value really differs: EF marks a
    /// property modified when it is assigned, even if the new value equals the old one.
    /// </summary>
    private static IEnumerable<PropertyEntry> AuditedProperties(EntityEntry entry, bool onlyChanged) =>
        entry.Properties
            .Where(property => !ExcludedProperties.Contains(property.Metadata.Name))
            .Where(property => !onlyChanged || (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue)));

    private static string EntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.DisplayName()} has no primary key to audit.");

        var parts = key.Properties.Select(property =>
        {
            var value = entry.Property(property.Name);

            // A key the database has not assigned yet (an identity column) is unknown here. Every
            // key in this model is generated in C#, so reaching this is a modelling mistake to
            // fix, not a row to record with a placeholder id.
            if (value.IsTemporary)
            {
                throw new InvalidOperationException(
                    $"{entry.Metadata.DisplayName()} has a database-generated key, which the audit log cannot record before saving.");
            }

            return Convert.ToString(value.CurrentValue, System.Globalization.CultureInfo.InvariantCulture);
        });

        return string.Join(',', parts);
    }

    private static string? Serialize(Dictionary<string, object?>? values) =>
        values is null ? null : JsonSerializer.Serialize(values, JsonOptions);
}

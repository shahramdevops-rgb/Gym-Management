namespace Gym.Domain.Audit;

/// <summary>
/// One row per inserted, updated or deleted entity: who changed what, when, and from where
/// (BUSINESS_RULES.md §11).
/// </summary>
/// <remarks>
/// <para>
/// Written only by the audit interceptor, never by a handler, so no use case can forget it.
/// Append-only: there are no methods that change a row, and a database trigger rejects UPDATE
/// and DELETE, so even code that bypasses this class cannot rewrite history.
/// </para>
/// <para>
/// Not an <c>Entity</c>: it has no audit fields of its own (it <i>is</i> the audit), and it
/// must never be audited itself, which would record every audit row forever.
/// </para>
/// </remarks>
public sealed class AuditLog
{
    // For EF Core.
    private AuditLog()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>Null for work done on nobody's behalf: login, seeding, background jobs.</summary>
    public Guid? UserId { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>The entity's name in the model, for example <c>User</c> or <c>RefreshToken</c>.</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>The primary key as text; composite keys are joined with a comma.</summary>
    public string EntityId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>JSON of the changed properties before the change. Null for an insert.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON of the changed properties after the change. Null for a delete.</summary>
    public string? NewValues { get; private set; }

    public string? IpAddress { get; private set; }

    public static AuditLog Record(
        AuditAction action,
        string entityType,
        string entityId,
        string? oldValues,
        string? newValues,
        Guid? userId,
        string? ipAddress,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            UserId = userId,
            IpAddress = ipAddress,
            OccurredAt = occurredAt,
        };
    }
}

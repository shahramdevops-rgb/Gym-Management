namespace Gym.Domain.Common;

/// <summary>
/// Base class for every persisted entity: identity plus the audit trail.
/// </summary>
/// <remarks>
/// Every setter is private, including the audit fields. Handlers never stamp timestamps and
/// entities never stamp themselves; the value is written by
/// <c>AuditableEntityInterceptor</c> in Gym.Infrastructure, which goes through EF Core's
/// change tracker and can therefore reach a private setter without this class opening up.
/// One place enforces the rule, so no use case can forget it.
/// </remarks>
public abstract class Entity
{
    /// <summary>
    /// A version 7 GUID, not a version 4 one. Version 7 puts a millisecond timestamp in the
    /// high bits, so freshly generated ids sort in creation order and inserts land at the end
    /// of the primary key index instead of scattering random writes across the whole B-tree.
    /// It is still generated in C# before the row reaches the database, which a serial column
    /// would not allow.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    /// When the row was inserted. UTC, stored as <c>timestamptz</c>: Npgsql rejects a
    /// <see cref="DateTimeOffset"/> whose offset is not zero, which is the point.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The user who inserted the row, from <c>ICurrentUser</c>. Null for rows written by
    /// anonymous requests, background jobs and seeding, which act on nobody's behalf.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Null until the row is updated for the first time.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>The user who last updated the row. See <see cref="CreatedBy"/>.</summary>
    public Guid? UpdatedBy { get; private set; }
}

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Common;

/// <summary>
/// A save broke a unique index. Thrown by <c>AppDbContext</c> in place of the provider's own
/// exception, so a handler can recognise the one race it expects without Application knowing
/// that the database is Postgres.
/// </summary>
/// <remarks>
/// <para>
/// The handler checks for a duplicate first and answers with a friendly error. This exception
/// covers what that check cannot: two requests passing it at the same moment, where only the
/// database can decide, and the loser must get the same answer instead of a 500.
/// </para>
/// <para>
/// It is a <see cref="DbUpdateException"/>, with the provider's exception still as its inner
/// exception, so code that catches the general case keeps working unchanged.
/// </para>
/// </remarks>
public sealed class UniqueConstraintException(string constraintName, DbUpdateException original)
    : DbUpdateException(
        $"Unique constraint {constraintName} was violated.",
        original?.InnerException,
        original?.Entries ?? [])
{
    /// <summary>The index name, for example <c>ix_members_phone_number</c>.</summary>
    public string ConstraintName { get; } = constraintName;
}

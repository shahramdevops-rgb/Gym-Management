using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Common;

/// <summary>
/// A save broke an exclusion constraint, such as two subscriptions of one member covering the
/// same date. Thrown by <c>AppDbContext</c> in place of the provider's exception, like
/// <see cref="UniqueConstraintException"/>, so a handler can answer the race it expects.
/// </summary>
/// <remarks>
/// The subscription constraint is deferred, so Postgres checks it when the transaction commits,
/// not when the row is written. The provider's error then arrives from the commit itself rather
/// than wrapped in a <see cref="DbUpdateException"/>, which is why this takes any exception as
/// its cause.
/// </remarks>
public sealed class ExclusionConstraintException(string constraintName, Exception providerException)
    : DbUpdateException($"Exclusion constraint {constraintName} was violated.", providerException)
{
    public string ConstraintName { get; } = constraintName;
}

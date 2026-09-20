using System.Linq.Expressions;

using Gym.Domain.Lockers;

namespace Gym.Application.Lockers;

/// <param name="IsOccupied">
/// Derived from an open attendance referencing this locker (BUSINESS_RULES.md §6). Always
/// <see langword="false"/> until task 5.2 adds the Attendance entity: there is no way yet for
/// any locker to be occupied, so this is the accurate answer, not a placeholder.
/// </param>
/// <param name="Version">Sent back with a status change, so a stale request is refused.</param>
public sealed record LockerResponse(
    Guid Id,
    int Number,
    bool IsOutOfService,
    bool IsOccupied,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL.</summary>
    public static readonly Expression<Func<Locker, LockerResponse>> Projection = locker => new LockerResponse(
        locker.Id,
        locker.Number,
        locker.IsOutOfService,
        false,
        locker.Version,
        locker.CreatedAt,
        locker.UpdatedAt);

    public static LockerResponse From(Locker locker)
    {
        ArgumentNullException.ThrowIfNull(locker);

        return new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            false,
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);
    }
}

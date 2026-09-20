using System.Linq.Expressions;

using Gym.Domain.Attendances;
using Gym.Domain.Lockers;

namespace Gym.Application.Lockers;

/// <param name="IsOccupied">Derived from an open attendance referencing this locker (BUSINESS_RULES.md §6).</param>
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
    /// <summary>
    /// The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL. Takes
    /// the open attendances as a queryable, not <c>IAppDbContext</c> directly, so the caller
    /// decides "open" once (<c>CheckedOutAt == null</c>) and this only correlates it by locker.
    /// </summary>
    public static Expression<Func<Locker, LockerResponse>> Projection(IQueryable<Attendance> openAttendances) =>
        locker => new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            openAttendances.Any(a => a.LockerId == locker.Id),
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);

    public static LockerResponse From(Locker locker, bool isOccupied)
    {
        ArgumentNullException.ThrowIfNull(locker);

        return new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            isOccupied,
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);
    }
}

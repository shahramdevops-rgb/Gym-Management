using System.Linq.Expressions;

using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;

namespace Gym.Application.Lockers;

/// <summary>The member an open attendance holds this locker for, or <c>null</c> when it is free.</summary>
public sealed record LockerHolder(Guid MemberId, string FullName);

/// <param name="OccupiedByMemberId">
/// Whoever the open attendance against this locker belongs to, or <c>null</c> when nobody holds
/// it. Derived, never stored (BUSINESS_RULES.md §6), for the same reason occupancy is: the open
/// attendance already says it, and a second copy could disagree with it.
/// </param>
/// <param name="Version">Sent back with a status change, so a stale request is refused.</param>
public sealed record LockerResponse(
    Guid Id,
    int Number,
    bool IsOutOfService,
    Guid? OccupiedByMemberId,
    string? OccupiedByMemberFullName,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// A locker is occupied exactly when somebody holds it, so this is read off the holder rather
    /// than carried beside it, where the two could drift apart.
    /// </summary>
    public bool IsOccupied => OccupiedByMemberId is not null;

    /// <summary>
    /// The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL. Takes
    /// the open attendances as a queryable, not <c>IAppDbContext</c> directly, so the caller
    /// decides "open" once (<c>CheckedOutAt == null</c>) and this only correlates it by locker.
    /// The members are correlated the same way, in the one query, rather than per row.
    /// </summary>
    public static Expression<Func<Locker, LockerResponse>> Projection(
        IQueryable<Attendance> openAttendances, IQueryable<Member> members) =>
        locker => new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            openAttendances
                .Where(a => a.LockerId == locker.Id)
                .Select(a => (Guid?)a.MemberId)
                .FirstOrDefault(),
            openAttendances
                .Where(a => a.LockerId == locker.Id)
                .SelectMany(a => members.Where(m => m.Id == a.MemberId).Select(m => m.FullName))
                .FirstOrDefault(),
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);

    public static LockerResponse From(Locker locker, LockerHolder? holder)
    {
        ArgumentNullException.ThrowIfNull(locker);

        return new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            holder?.MemberId,
            holder?.FullName,
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);
    }
}

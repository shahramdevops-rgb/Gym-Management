using System.Linq.Expressions;

using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;

namespace Gym.Application.Lockers;

/// <summary>
/// Who an open attendance holds this locker for: a member, or a guest by name (BUSINESS_RULES.md §7
/// <i>Guest visit</i>). <c>null</c> as a whole when the locker is free.
/// </summary>
public sealed record LockerHolder(Guid? MemberId, string? FullName, string? GuestName, bool IsCardioOnly);

/// <param name="OccupiedByMemberId">
/// Whoever the open attendance against this locker belongs to, or <c>null</c> when nobody holds
/// it or a guest does. Derived, never stored (BUSINESS_RULES.md §6), for the same reason occupancy
/// is: the open attendance already says it, and a second copy could disagree with it.
/// </param>
/// <param name="OccupiedByGuestName">
/// The guest's name when a guest holds the locker (§6, §7 <i>Guest visit</i>): the map draws the
/// door in the guest colour and writes the name on it. <c>null</c> otherwise.
/// </param>
/// <param name="OccupiedOnCardioOnly">
/// The member holding the locker came in only for هوازی (§7 <i>Cardio-only visit</i>): the map draws
/// the door in the cardio colour. <c>false</c> for a free locker and a guest's.
/// </param>
/// <param name="Version">Sent back with a status change, so a stale request is refused.</param>
/// <param name="HolderDebt">
/// What the holder still owes, so the map can mark their door "بدهکار" (§6): a member's whole debt
/// (§5 <i>Member debt</i>), or for a guest what the visit's cafe orders still owe. <c>0</c> for a
/// free locker. Computed only by <see cref="ListLockers.ListLockersHandler"/>, the map's one read;
/// every other path leaves it <c>0</c>, the way <see cref="Members.MemberResponse.Debt"/> is
/// filled only by the member list.
/// </param>
public sealed record LockerResponse(
    Guid Id,
    int Number,
    bool IsOutOfService,
    Guid? OccupiedByMemberId,
    string? OccupiedByMemberFullName,
    string? OccupiedByGuestName,
    bool OccupiedOnCardioOnly,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    decimal HolderDebt = 0)
{
    /// <summary>
    /// A locker is occupied exactly when somebody holds it, a member or a guest, so this is read off
    /// the holder rather than carried beside it, where the two could drift apart.
    /// </summary>
    public bool IsOccupied => OccupiedByMemberId is not null || OccupiedByGuestName is not null;

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
                .Select(a => a.MemberId)
                .FirstOrDefault(),
            openAttendances
                .Where(a => a.LockerId == locker.Id)
                .SelectMany(a => members.Where(m => m.Id == a.MemberId).Select(m => m.FullName))
                .FirstOrDefault(),
            openAttendances
                .Where(a => a.LockerId == locker.Id)
                .Select(a => a.GuestName)
                .FirstOrDefault(),
            openAttendances.Any(a => a.LockerId == locker.Id && a.IsCardioOnly),
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt,
            // An expression tree cannot leave an optional argument out; the list fills it in after.
            0m);

    public static LockerResponse From(Locker locker, LockerHolder? holder)
    {
        ArgumentNullException.ThrowIfNull(locker);

        return new LockerResponse(
            locker.Id,
            locker.Number,
            locker.IsOutOfService,
            holder?.MemberId,
            holder?.FullName,
            holder?.GuestName,
            holder?.IsCardioOnly ?? false,
            locker.Version,
            locker.CreatedAt,
            locker.UpdatedAt);
    }
}

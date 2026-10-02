using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.ListLockerVisitsToday;

/// <summary>
/// Everyone who had a locker today, oldest first (BUSINESS_RULES.md §6 <i>Who had a locker
/// today</i>, roadmap 6.5.10).
/// </summary>
/// <remarks>
/// A visit is matched by the locker it holds now. A move overwrites <c>Attendance.LockerId</c>, so
/// a visit moved away is listed under the locker it went to, which is what the rule says. Cancelled
/// visits are included, like a member's own history.
/// <para>
/// Not paged: one locker in one day is a handful of visits, bounded by how many people can take
/// turns with one key between opening and closing.
/// </para>
/// </remarks>
public sealed class ListLockerVisitsTodayHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<IReadOnlyList<LockerVisitResponse>>> Handle(Guid lockerId, CancellationToken cancellationToken)
    {
        var lockerExists = await db.Lockers.AnyAsync(l => l.Id == lockerId, cancellationToken);
        if (!lockerExists)
        {
            return Result.Failure<IReadOnlyList<LockerVisitResponse>>(LockerErrors.NotFound);
        }

        var startOfToday = calendar.StartOfDayUtc(calendar.Today());
        var members = db.Members;

        var visits = await db.Attendances
            .AsNoTracking()
            .Where(a => a.LockerId == lockerId && a.CheckedInAt >= startOfToday)
            .OrderBy(a => a.CheckedInAt)
            .ThenBy(a => a.Id)
            .Select(a => new LockerVisitResponse(
                a.Id,
                a.MemberId,
                members.Where(m => m.Id == a.MemberId).Select(m => m.FullName).FirstOrDefault(),
                a.GuestName,
                a.CheckedInAt,
                a.CheckedOutAt,
                a.CancelledAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LockerVisitResponse>>(visits);
    }
}

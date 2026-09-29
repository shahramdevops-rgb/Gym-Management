using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Lockers.LockerUsage;

/// <summary>
/// Visits per locker over the last 7, 30 or 90 days, for the map's usage view (BUSINESS_RULES.md §6
/// <i>Locker usage map</i>, roadmap 6.5.15).
/// </summary>
/// <remarks>
/// One SQL statement: every locker with a count of its visits whose <c>checked_in_at</c> falls
/// between the gym's midnight at the start of the period and the midnight after today, served by
/// the index on that column. Starting from the lockers rather than grouping the visits keeps a
/// locker nobody used in the answer, with 0, which is exactly the locker the view is looking for.
/// <para>
/// A visit is matched by the locker it holds now. A move overwrites <c>Attendance.LockerId</c>, so a
/// visit moved away counts for the locker it went to, which is what the rule says (the same as
/// <see cref="ListLockerVisitsToday.ListLockerVisitsTodayHandler"/>).
/// </para>
/// </remarks>
public sealed class LockerUsageHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<LockerUsageResponse> Handle(LockerUsageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = calendar.Today();
        var from = today.AddDays(1 - query.Days);
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(today.AddDays(1));
        var attendances = db.Attendances;

        var lockers = await db.Lockers
            .AsNoTracking()
            .OrderBy(locker => locker.Number)
            .Select(locker => new LockerUseCountResponse(
                locker.Id,
                locker.Number,
                attendances.Count(a =>
                    a.LockerId == locker.Id
                    && a.CheckedInAt >= start
                    && a.CheckedInAt < end
                    && a.CancelledAt == null)))
            .ToListAsync(cancellationToken);

        return new LockerUsageResponse(from, today, query.Days, lockers);
    }
}

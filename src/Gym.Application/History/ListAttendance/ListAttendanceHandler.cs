using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.History.ListAttendance;

/// <summary>
/// Every check-in in the gym, newest first (BUSINESS_RULES.md §12 <i>History</i>, roadmap 6.5.25),
/// with who recorded it. No date limit for either role. Cancelled and auto-closed visits are
/// listed and marked, the same choice a member's own visit history makes: this is the record of
/// what happened at the desk, not the attendance figure the reports count. Guests' visits are
/// listed too, under the name typed at the desk; the reports count them nowhere (§7 <i>Guest visit</i>).
/// </summary>
public sealed class ListAttendanceHandler(IAppDbContext db, IGymCalendar calendar, IUserNames users)
{
    public async Task<PagedResponse<HistoryAttendanceResponse>> Handle(
        ListAttendanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var attendances = db.Attendances.AsNoTracking();

        if (query.MemberId is { } memberId)
        {
            attendances = attendances.Where(attendance => attendance.MemberId == memberId);
        }

        // A visit belongs to the day it began, in the gym's time zone (§12): the dates become a
        // range of moments, which the index on checked_in_at answers.
        if (query.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            attendances = attendances.Where(attendance => attendance.CheckedInAt >= start);
        }

        if (query.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            attendances = attendances.Where(attendance => attendance.CheckedInAt < end);
        }

        var totalCount = await attendances.CountAsync(cancellationToken);

        // Id breaks ties so paging never repeats or skips a row.
        var page = await attendances
            .OrderByDescending(attendance => attendance.CheckedInAt)
            .ThenByDescending(attendance => attendance.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(attendance => new
            {
                attendance.Id,
                attendance.MemberId,
                attendance.GuestName,
                MemberFullName = db.Members
                    .Where(member => member.Id == attendance.MemberId)
                    .Select(member => member.FullName)
                    .FirstOrDefault(),
                LockerNumber = db.Lockers
                    .Where(locker => locker.Id == attendance.LockerId)
                    .Select(locker => (int?)locker.Number)
                    .FirstOrDefault(),
                UsesReservePlace = attendance.ReserveSlot != null,
                attendance.CheckedInAt,
                attendance.CheckedOutAt,
                attendance.CancelledAt,
                attendance.AutoClosedAt,
                attendance.CreatedBy,
            })
            .ToListAsync(cancellationToken);

        var names = await users.FullNamesAsync(
            page.Where(row => row.CreatedBy is not null).Select(row => row.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        var items = page
            .Select(row => new HistoryAttendanceResponse(
                row.Id,
                row.MemberId,
                row.MemberFullName,
                row.GuestName,
                row.LockerNumber,
                row.UsesReservePlace,
                row.CheckedInAt,
                row.CheckedOutAt,
                row.CancelledAt,
                row.AutoClosedAt,
                row.CreatedBy is { } userId ? names.GetValueOrDefault(userId) : null))
            .ToList();

        return new PagedResponse<HistoryAttendanceResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

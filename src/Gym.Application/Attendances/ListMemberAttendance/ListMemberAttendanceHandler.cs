using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.ServiceCharges;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Attendances.ListMemberAttendance;

/// <summary>
/// A member's attendance history, optionally filtered by date range (roadmap 5.3). Cancelled
/// visits are included, marked by <see cref="AttendanceResponse.CancelledAt"/> — unlike the
/// Phase 9 aggregate reports, which BUSINESS_RULES.md §7/§12 exclude them from, this is an
/// operational view of one member's visits, not a statistic.
/// </summary>
public sealed class ListMemberAttendanceHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<PagedResponse<AttendanceResponse>>> Handle(
        Guid memberId, ListMemberAttendanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberExists = await db.Members.AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            return Result.Failure<PagedResponse<AttendanceResponse>>(MemberErrors.NotFound);
        }

        var attendances = db.Attendances.AsNoTracking().Where(a => a.MemberId == memberId);

        if (query.From is { } from)
        {
            var start = calendar.StartOfDayUtc(from);
            attendances = attendances.Where(a => a.CheckedInAt >= start);
        }

        if (query.To is { } to)
        {
            var end = calendar.StartOfDayUtc(to.AddDays(1));
            attendances = attendances.Where(a => a.CheckedInAt < end);
        }

        var totalCount = await attendances.CountAsync(cancellationToken);

        var items = await attendances
            .OrderByDescending(a => a.CheckedInAt)
            .ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(AttendanceResponse.Projection(db.Lockers))
            .ToListAsync(cancellationToken);

        var chargesByVisit = await VisitServiceCharges.ByAttendanceAsync(
            db, items.Select(item => item.Id).ToList(), cancellationToken);

        var rows = items
            .Select(item => item with { ServiceCharges = chargesByVisit.GetValueOrDefault(item.Id, []) })
            .ToList();

        return new PagedResponse<AttendanceResponse>(rows, query.Page, query.PageSize, totalCount);
    }
}


using Gym.Application.Common;
using Gym.Domain.Attendances;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetAttendanceReport;

/// <summary>
/// Members' visits per day and by weekday and hour, compared with the range before
/// (BUSINESS_RULES.md §12 <i>Operational reports</i>, roadmap 9.2). Owner only; the endpoint's policy
/// says so.
/// </summary>
/// <remarks>
/// The range's check-ins come back as moments and members, and are put into days and hours here,
/// in the gym's time zone, the way the chart under the map does (<c>TodayByHourHandler</c>). A year
/// of one gym's visits is a few tens of thousands of small rows. The range before is only counted,
/// so the database does that.
/// </remarks>
public sealed class GetAttendanceReportHandler(IAppDbContext db, IGymCalendar calendar)
{
    private const int HoursInDay = 24;

    /// <summary>The Iranian week: Saturday to Friday.</summary>
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
        DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    ];

    public async Task<AttendanceReportResponse> Handle(GetAttendanceReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The validator has made sure both are there, in order, and at most a year apart.
        var from = query.From!.Value;
        var to = query.To!.Value;
        var length = to.DayNumber - from.DayNumber + 1;

        var visits = await Counted(from, to)
            .Select(attendance => new { attendance.CheckedInAt, attendance.MemberId })
            .ToListAsync(cancellationToken);
        var previousVisits = await Counted(from.AddDays(-length), from.AddDays(-1)).CountAsync(cancellationToken);

        var perDay = new Dictionary<DateOnly, int>();
        var byWeekday = WeekOrder.ToDictionary(weekday => weekday, _ => new int[HoursInDay]);
        foreach (var visit in visits)
        {
            var local = TimeZoneInfo.ConvertTime(visit.CheckedInAt, calendar.TimeZone);
            var day = DateOnly.FromDateTime(local.DateTime);
            perDay[day] = perDay.GetValueOrDefault(day) + 1;
            byWeekday[local.DayOfWeek][local.Hour]++;
        }

        return new AttendanceReportResponse(
            from,
            to,
            visits.Count,
            previousVisits,
            visits.Select(visit => visit.MemberId).Distinct().Count(),
            Enumerable.Range(0, length)
                .Select(offset => from.AddDays(offset))
                .Select(day => new AttendanceDayResponse(day, perDay.GetValueOrDefault(day)))
                .ToList(),
            WeekOrder.Select(weekday => new AttendanceWeekdayResponse(weekday, byWeekday[weekday])).ToList());
    }

    /// <summary>
    /// The range's members' check-ins, by the moment they began. Cancelled ones are not attendance
    /// (§12), and neither is a guest (§7 <i>Guest visit</i>): counting guests would make the gym look
    /// busier than its members make it.
    /// </summary>
    private IQueryable<Attendance> Counted(DateOnly from, DateOnly to)
    {
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(to.AddDays(1));

        return db.Attendances
            .AsNoTracking()
            .Where(attendance => attendance.CheckedInAt >= start && attendance.CheckedInAt < end &&
                attendance.CancelledAt == null && attendance.MemberId != null);
    }
}

using Gym.Application.Common;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetTodaySummary;

/// <summary>
/// Today's summary for the Owner's phone (BUSINESS_RULES.md §12 <i>Today's summary</i>, roadmap
/// 13.5). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// <b>The dashboard's own handlers give the money and the visits</b>, asked for a range of one day,
/// as the daily report SMS asks them (<c>DailyReportCandidate</c>). So the phone, the SMS and the
/// dashboard can never disagree about today: a rule changed in one report changes all three. Only
/// "inside now" is counted here, the way the «داخل باشگاه» board counts it: every visit not yet
/// closed. A cancelled visit and one the nightly job closed have a check-out, so neither is inside.
/// </remarks>
public sealed class GetTodaySummaryHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    GetFinancialReportHandler financial,
    GetAttendanceReportHandler attendance)
{
    public async Task<TodaySummaryResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();

        var money = (await financial.Handle(new GetFinancialReportQuery(today, today), cancellationToken)).Current;
        var visits = await attendance.Handle(new GetAttendanceReportQuery(today, today), cancellationToken);

        // Grouping on a constant turns the two counts into one SELECT; nobody inside means no group.
        var inside = await db.Attendances
            .AsNoTracking()
            .Where(visit => visit.CheckedOutAt == null)
            .GroupBy(visit => 1)
            .Select(open => new
            {
                Everyone = open.Count(),
                Guests = open.Count(visit => visit.MemberId == null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        int Sold(RevenueSource source) =>
            money.BySource.Single(row => row.Source == source).Sold;

        return new TodaySummaryResponse(
            today,
            inside?.Everyone ?? 0,
            inside?.Guests ?? 0,
            visits.Visits,
            money.ReceivedByMethod.Sum(row => row.Money.Net),
            Sold(RevenueSource.Membership),
            Sold(RevenueSource.SingleSession));
    }
}

using Gym.Application.Common;
using Gym.Application.Reports;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Domain.Notifications;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// The Owner's daily report for today, built but not yet saved (BUSINESS_RULES.md §10 <i>The daily
/// report</i>), or nothing when today has no figures or already has its report.
/// </summary>
/// <remarks>
/// <b>The dashboard's own handlers give the figures.</b> The report is asked to say what the dashboard
/// says for the day, so rather than a second query that would have to repeat which payments count as
/// «دریافتی» and which visits as attendance, it asks the same two reports for a range of one day.
/// When a rule of the dashboard changes, the SMS changes with it. The price is that the financial
/// report also works out the day before, to compare with, which for one day is a few rows more.
/// </remarks>
public sealed class DailyReportCandidate(
    IAppDbContext db, GetFinancialReportHandler financial, GetAttendanceReportHandler attendance)
{
    public async Task<List<Notification>> FindAsync(SmsSettings settings, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The database holds "on means filled", and a run only starts for a kind that is on.
        var phone = settings.DailyReportPhone
            ?? throw new InvalidOperationException("The daily report is on with no number.");

        // Left out here to save the run from trying; the unique index is what really refuses it.
        var alreadyWritten = await db.Notifications.AnyAsync(
            notification => notification.Kind == NotificationKind.DailyReport && notification.ReportDate == today,
            cancellationToken);
        if (alreadyWritten)
        {
            return [];
        }

        var figures = await FiguresAsync(today, cancellationToken);
        if (figures.IsEmpty())
        {
            return [];
        }

        return [Notification.ForDailyReport(today, phone, SmsText.ForDailyReport(figures))];
    }

    /// <summary>One day of the dashboard: its «دریافتی», its sales and its attendance.</summary>
    public async Task<DailyReportFigures> FiguresAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var money = (await financial.Handle(new GetFinancialReportQuery(day, day), cancellationToken)).Current;
        var visits = await attendance.Handle(new GetAttendanceReportQuery(day, day), cancellationToken);

        decimal Received(PaymentMethod method) =>
            money.ReceivedByMethod.Single(row => row.Method == method).Money.Net;

        int Sold(RevenueSource source) =>
            money.BySource.Single(row => row.Source == source).Sold;

        return new DailyReportFigures(
            day,
            visits.Visits,
            Sold(RevenueSource.Membership),
            Sold(RevenueSource.SingleSession),
            money.ReceivedByMethod.Sum(row => row.Money.Net),
            Received(PaymentMethod.Cash),
            Received(PaymentMethod.Card),
            Received(PaymentMethod.BankTransfer));
    }
}

using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// One kind's daily SMS run (BUSINESS_RULES.md §10 <i>The daily runs</i>): finds who is due the
/// message today and sends it to each, once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, then sent, one at a time</b>, with retries after everyone else and a stop when the
/// credit is used up: <see cref="SmsBatchSender"/>, which the broadcast shares. The unique indexes
/// refuse a second row for the same event, so a second run, or a run racing this one, cannot pay for
/// it again. If the app stops between a row's two saves, the next run of the kind finds it still
/// <c>Pending</c> and marks it <c>Unknown</c>: the request may have left, and it carries no
/// duplicate guard.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire at the kind's send time,
/// and never two runs at once.
/// </para>
/// </remarks>
public sealed partial class SendDailySmsHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    SmsBatchSender batch,
    DailyReportCandidate dailyReport,
    DebtReminderCandidate debtReminders,
    AbsenceCandidate absences,
    ILogger<SendDailySmsHandler> logger)
{
    public async Task<SmsRunResult> Handle(NotificationKind kind, CancellationToken cancellationToken)
    {
        // The receipt is sent at each payment (SendSubscriptionReceiptHandler), the welcome at the
        // sale of a first plan (SendWelcomeHandler), the welcome back after a check-in
        // (SendWelcomeBackHandler) and the broadcast from its page (RunBroadcastHandler), never by a
        // daily run.
        if (kind is NotificationKind.SubscriptionReceipt or NotificationKind.Welcome or NotificationKind.WelcomeBack
            or NotificationKind.Broadcast)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "This kind has no daily run.");
        }

        // Read now, not when the run was scheduled: a change applies from the next run (§10).
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.For(kind).Enabled)
        {
            LogNotRun(logger, kind, "SMS or this kind is off");
            return SmsRunResult.NotRun;
        }

        if (!SmsSettings.IsWithinSendingHours(kind, calendar.TimeOfDay()))
        {
            LogNotRun(logger, kind, "outside the sending hours");
            return SmsRunResult.NotRun;
        }

        var interrupted = await MarkInterruptedAsync(kind, cancellationToken);

        var today = calendar.Today();
        var candidates = kind switch
        {
            NotificationKind.DailyReport => await dailyReport.FindAsync(settings, today, cancellationToken),
            NotificationKind.DebtReminder => await debtReminders.FindAsync(settings, today, cancellationToken),
            NotificationKind.Absence => await absences.FindAsync(settings, today, cancellationToken),
            _ => await SmsCandidates.FindAsync(db, kind, settings, today, cancellationToken),
        };

        // §10: credit used up stops the run; the ones not written yet stay unwritten, so tomorrow's
        // run still reaches whoever is still covered.
        var sent = await batch.SendAsync(kind, candidates, cancellationToken);

        LogRun(logger, kind, sent.Sent, sent.Failed, sent.Unknown, interrupted, sent.CreditExhausted);

        return new SmsRunResult(Ran: true, sent.Sent, sent.Failed, sent.Unknown, interrupted, sent.CreditExhausted);
    }

    /// <summary>Rows a stopped run left <c>Pending</c>. Runs never overlap, so any found now are left over.</summary>
    private async Task<int> MarkInterruptedAsync(NotificationKind kind, CancellationToken cancellationToken)
    {
        var leftOver = await db.Notifications
            .Where(notification => notification.Kind == kind && notification.Status == NotificationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var notification in leftOver)
        {
            notification.MarkInterrupted();
        }

        if (leftOver.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return leftOver.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS run {Kind} not made: {Reason}")]
    private static partial void LogNotRun(ILogger logger, NotificationKind kind, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMS run {Kind}: {Sent} sent, {Failed} failed, {Unknown} unknown, {Interrupted} left over from a stopped run, credit used up: {CreditExhausted}")]
    private static partial void LogRun(
        ILogger logger, NotificationKind kind, int sent, int failed, int unknown, int interrupted, bool creditExhausted);
}

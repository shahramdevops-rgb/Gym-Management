using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendWelcomeBack;

/// <summary>
/// Asks for a member's welcome back when they check in for the first time after their absence SMS
/// (BUSINESS_RULES.md §10 <i>The welcome back</i>, task 10.20): half an hour later, not before 08:00,
/// and never once that falls after midnight. Called by both check-ins, the ordinary one and the
/// cardio-only one, after their transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>After the commit, never inside it.</b> The job must find the visit when it runs, and a check-in
/// rolled back must not leave a welcome back behind.
/// </para>
/// <para>
/// <b>A welcome back never fails a check-in.</b> The visit is already saved when this runs. If the
/// job runner cannot take it, that one SMS is lost and logged, as a welcome would be.
/// </para>
/// </remarks>
public sealed partial class WelcomeBacks(
    IAppDbContext db,
    IGymCalendar calendar,
    IWelcomeBackScheduler scheduler,
    ILogger<WelcomeBacks> logger)
{
    public async Task QueueIfFirstVisitAsync(Guid attendanceId, CancellationToken cancellationToken)
    {
        // §10: only while it is on when the member checks in; turning it on later reaches no one.
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.WelcomeBackEnabled)
        {
            return;
        }

        var visit = await FindAsync(db, calendar, attendanceId, cancellationToken);
        if (visit is not { Earned: true })
        {
            return;
        }

        if (SmsSettings.WelcomeBackAt(visit.CheckInDay, visit.CheckInTime) is not { } at)
        {
            return;
        }

        try
        {
            Queue(attendanceId, at);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotQueued(logger, exception, attendanceId);
        }
    }

    /// <summary>
    /// At this local moment in the gym's zone. The job calls it too, for a welcome back whose time
    /// came before 08:00.
    /// </summary>
    public void Queue(Guid attendanceId, DateTime localMoment)
    {
        var at = TimeZoneInfo.ConvertTimeToUtc(localMoment, calendar.TimeZone);
        scheduler.SendAt(attendanceId, new DateTimeOffset(at, TimeSpan.Zero));
    }

    /// <summary>
    /// The check-in and the absence it may end, read as they stand now; <c>null</c> when the visit is
    /// not a member's or the member never had an absence SMS. Whether it earns the welcome back is
    /// <see cref="SmsAudience.EarnsWelcomeBack"/>'s answer.
    /// </summary>
    internal static async Task<WelcomeBackVisit?> FindAsync(
        IAppDbContext db, IGymCalendar calendar, Guid attendanceId, CancellationToken cancellationToken)
    {
        var visit = await db.Attendances.AsNoTracking()
            .Where(attendance => attendance.Id == attendanceId && attendance.MemberId != null)
            .Select(attendance => new { MemberId = attendance.MemberId!.Value, attendance.CheckedInAt, attendance.CancelledAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (visit is null)
        {
            return null;
        }

        // The member's latest absence SMS, whatever became of it (§10: an absence is named by its day).
        var absence = await db.Notifications.AsNoTracking()
            .Where(notification => notification.Kind == NotificationKind.Absence && notification.MemberId == visit.MemberId)
            .OrderByDescending(notification => notification.AbsentSince)
            .Select(notification => new { AbsentSince = notification.AbsentSince!.Value, notification.Status, notification.Delivery, notification.SentAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (absence is null)
        {
            return null;
        }

        DateTimeOffset? firstVisitSinceAt = null;
        if (absence.SentAt is { } sentAt)
        {
            // A cancelled check-in is not a visit; a cardio-only one is (§10 The absence SMS).
            firstVisitSinceAt = await db.Attendances.AsNoTracking()
                .Where(attendance => attendance.MemberId == visit.MemberId
                    && attendance.CancelledAt == null
                    && attendance.CheckedInAt > sentAt)
                .OrderBy(attendance => attendance.CheckedInAt)
                .Select(attendance => (DateTimeOffset?)attendance.CheckedInAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var earned = SmsAudience.EarnsWelcomeBack(
            absence.Status,
            absence.Delivery,
            absence.SentAt,
            visit.CheckedInAt,
            visitCancelled: visit.CancelledAt is not null,
            firstVisitSinceAt);

        var local = TimeZoneInfo.ConvertTime(visit.CheckedInAt, calendar.TimeZone).DateTime;

        return new WelcomeBackVisit(
            visit.MemberId,
            absence.AbsentSince,
            DateOnly.FromDateTime(local),
            TimeOnly.FromDateTime(local),
            earned);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The welcome back for attendance {AttendanceId} could not be queued; it will not be sent")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid attendanceId);
}

/// <param name="AbsentSince">The day the member's latest absence SMS counted from: the welcome back's event.</param>
/// <param name="CheckInDay">The gym's day of the check-in.</param>
/// <param name="CheckInTime">The gym's time of day of the check-in.</param>
/// <param name="Earned">Whether the check-in earns the welcome back (<see cref="SmsAudience.EarnsWelcomeBack"/>).</param>
internal sealed record WelcomeBackVisit(Guid MemberId, DateOnly AbsentSince, DateOnly CheckInDay, TimeOnly CheckInTime, bool Earned);

using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendWelcomeBack;

/// <summary>
/// Sends one member's welcome back (BUSINESS_RULES.md §10 <i>The welcome back</i>, task 10.20), asked
/// for by <see cref="WelcomeBacks"/> half an hour after their first visit since the absence SMS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is checked again here.</b> In that half hour the Owner may have turned it off, the
/// desk may have cancelled the check-in (and made it again: the new one asks for its own), or the
/// member may have been deactivated.
/// </para>
/// <para>
/// <b>The text is the member's turn.</b> The number of welcome backs they already have, whatever
/// became of them, picks the next of <see cref="SmsText.WelcomeBackTexts"/>.
/// </para>
/// <para>
/// <b>Written, then sent, like the welcome.</b> The row is saved <c>Pending</c> before the request,
/// and the unique index on the member and the absence refuses a second one. If the app stops
/// between the two saves, Hangfire makes the job again once the app is back; it then finds the row
/// still <c>Pending</c> and marks it <c>Unknown</c>, since the request may have left.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one at a time.
/// </para>
/// </remarks>
public sealed partial class SendWelcomeBackHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    WelcomeBacks welcomeBacks,
    ILogger<SendWelcomeBackHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.WelcomeBack;

    /// <param name="attendanceId">The check-in that was the member's first visit after the absence SMS.</param>
    public async Task<WelcomeBackOutcome> Handle(Guid attendanceId, CancellationToken cancellationToken)
    {
        var outcome = await SendAsync(attendanceId, cancellationToken);
        LogOutcome(logger, attendanceId, outcome);

        return outcome;
    }

    private async Task<WelcomeBackOutcome> SendAsync(Guid attendanceId, CancellationToken cancellationToken)
    {
        var visit = await WelcomeBacks.FindAsync(db, calendar, attendanceId, cancellationToken);
        if (visit is null)
        {
            return WelcomeBackOutcome.NoLongerDue;
        }

        // Welcome backs run one at a time, so this absence's row found Pending was left by a job that stopped.
        var written = await db.Notifications
            .SingleOrDefaultAsync(
                notification => notification.MemberId == visit.MemberId
                    && notification.Kind == Kind
                    && notification.AbsentSince == visit.AbsentSince,
                cancellationToken);
        if (written is not null)
        {
            if (written.MarkInterrupted().IsFailure)
            {
                return WelcomeBackOutcome.AlreadyWritten;
            }

            await db.SaveChangesAsync(cancellationToken);
            return WelcomeBackOutcome.Interrupted;
        }

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.WelcomeBackEnabled)
        {
            return WelcomeBackOutcome.Off;
        }

        // A job whose time came while the server was down may run the next day: it is about today only.
        if (calendar.Today() != visit.CheckInDay)
        {
            return WelcomeBackOutcome.TooLate;
        }

        // The same day and outside its hours (08:00 until midnight) is before 08:00: it waits for 08:00.
        if (!SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay()))
        {
            welcomeBacks.Queue(attendanceId, visit.CheckInDay.ToDateTime(SmsSettings.EarliestSendTime, DateTimeKind.Unspecified));
            return WelcomeBackOutcome.Deferred;
        }

        if (await BuildAsync(visit, cancellationToken) is not { } notification)
        {
            return WelcomeBackOutcome.NoLongerDue;
        }

        if (!await TryWriteAsync(notification, cancellationToken))
        {
            return WelcomeBackOutcome.AlreadyWritten;
        }

        return await SendWithRetriesAsync(notification, cancellationToken);
    }

    /// <summary>The message, or <c>null</c> when the welcome back is no longer due.</summary>
    private async Task<Notification?> BuildAsync(WelcomeBackVisit visit, CancellationToken cancellationToken)
    {
        if (!visit.Earned)
        {
            return null;
        }

        var member = await db.Members.AsNoTracking()
            .Where(member => member.Id == visit.MemberId)
            .Select(member => new { member.FullName, member.PhoneNumber, member.IsActive })
            .SingleAsync(cancellationToken);
        if (!member.IsActive)
        {
            return null;
        }

        var turn = await db.Notifications.AsNoTracking()
            .CountAsync(notification => notification.MemberId == visit.MemberId && notification.Kind == Kind, cancellationToken);

        return Notification.ForWelcomeBack(
            visit.MemberId, visit.AbsentSince, member.PhoneNumber, SmsText.ForWelcomeBack(member.FullName, turn));
    }

    /// <summary>Saves the row before its request. <c>false</c> when this absence already has one.</summary>
    private async Task<bool> TryWriteAsync(Notification notification, CancellationToken cancellationToken)
    {
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (UniqueConstraintException)
        {
            db.Notifications.Remove(notification);
            return false;
        }
    }

    /// <summary>A failure that may pass is tried again, as in a run, while the tries and the hours last.</summary>
    private async Task<WelcomeBackOutcome> SendWithRetriesAsync(Notification notification, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        while (outcome == SmsSendOutcome.RetryableFailure)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), Kind) is not { } delay)
            {
                notification.GiveUp();
                await db.SaveChangesAsync(CancellationToken.None);
                return WelcomeBackOutcome.Failed;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;
            outcome = await SmsRequest.SendAsync(db, sender, time, notification);
        }

        return outcome switch
        {
            SmsSendOutcome.Sent => WelcomeBackOutcome.Sent,
            SmsSendOutcome.Unknown => WelcomeBackOutcome.Unknown,
            _ => WelcomeBackOutcome.Failed,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Welcome back for attendance {AttendanceId}: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid attendanceId, WelcomeBackOutcome outcome);
}

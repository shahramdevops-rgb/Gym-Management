using Gym.Application.Bookings;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Bookings;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendSessionCancelledSms;

/// <summary>
/// Sends one student's SMS about the sessions a trainer's save cancelled (BUSINESS_RULES.md §15
/// <i>Timetable and attendance</i>, task 15.6c), asked for by <see cref="SessionCancelledSms"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is checked again here.</b> The job may run hours after the save (one made at 23:00
/// goes at 08:00), and by then the Owner may have turned the SMS off, the trainer may have brought the
/// session back, the session may have started, or the student may have seen it in the app. Only a
/// session still cancelled, not started, not seen and within
/// <see cref="SmsSettings.SessionCancelledDaysAhead"/> days is worth an SMS.
/// </para>
/// <para>
/// <b>Written, then sent, like the trainer's SMS.</b> The row is saved <c>Pending</c> before the
/// request, and the unique index on member and change refuses a second one.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one at a time.
/// </para>
/// </remarks>
public sealed partial class SendSessionCancelledSmsHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    SessionCancelledSms sessionCancelledSms,
    ILogger<SendSessionCancelledSmsHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.SessionCancelled;

    public async Task<SessionCancelledSmsOutcome> Handle(Guid changeId, Guid memberId, CancellationToken cancellationToken)
    {
        var outcome = await SendAsync(changeId, memberId, cancellationToken);
        LogOutcome(logger, changeId, memberId, outcome);

        return outcome;
    }

    private async Task<SessionCancelledSmsOutcome> SendAsync(Guid changeId, Guid memberId, CancellationToken cancellationToken)
    {
        // These jobs run one at a time, so a row found Pending was left by a job that stopped.
        var written = await db.Notifications
            .SingleOrDefaultAsync(
                notification => notification.Kind == Kind
                    && notification.MemberId == memberId
                    && notification.TimetableChangeId == changeId,
                cancellationToken);
        if (written is not null)
        {
            if (written.MarkInterrupted().IsFailure)
            {
                return SessionCancelledSmsOutcome.AlreadyWritten;
            }

            await db.SaveChangesAsync(cancellationToken);
            return SessionCancelledSmsOutcome.Interrupted;
        }

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.SessionCancelledEnabled)
        {
            return SessionCancelledSmsOutcome.Off;
        }

        // A job whose time came while the server was down may run at night: it waits for 08:00.
        if (!SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay()))
        {
            sessionCancelledSms.Queue(changeId, memberId);
            return SessionCancelledSmsOutcome.Deferred;
        }

        if (await BuildAsync(changeId, memberId, cancellationToken) is not { } notification)
        {
            return SessionCancelledSmsOutcome.NoLongerDue;
        }

        if (!await TryWriteAsync(notification, cancellationToken))
        {
            return SessionCancelledSmsOutcome.AlreadyWritten;
        }

        return await SendWithRetriesAsync(notification, cancellationToken);
    }

    /// <summary>The message, or <c>null</c> when it is no longer due.</summary>
    private async Task<Notification?> BuildAsync(Guid changeId, Guid memberId, CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();

        var rows = await db.SessionCancellations.AsNoTracking()
            .Where(row => row.ChangeId == changeId && row.MemberId == memberId && row.Date >= today)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        var trainerId = rows[0].TrainerId;
        var linked = await db.TrainerLinks.AnyAsync(
            link => link.TrainerId == trainerId && link.MemberId == memberId && link.EndedOn == null, cancellationToken);
        if (!linked)
        {
            return null;
        }

        var member = await db.Members.AsNoTracking()
            .Where(row => row.Id == memberId)
            .Select(row => new { row.FullName, row.PhoneNumber, row.IsActive })
            .SingleAsync(cancellationToken);
        if (!member.IsActive)
        {
            return null;
        }

        // The others are counted as the app lists them: still cancelled and not seen, any date ahead.
        var stillCancelled = (await StillCancelled.FilterAsync(db, trainerId, rows, cancellationToken))
            .Where(row => row.SeenAt is null && !BookingRules.HasStarted(row.Date, row.Start, today, timeOfDay))
            .OrderBy(row => row.Date).ThenBy(row => row.Start).ThenBy(row => row.End)
            .ToList();
        if (stillCancelled.FirstOrDefault(row => row.IsSmsDue(today, timeOfDay, SmsSettings.SessionCancelledDaysAhead)) is not { } nearest)
        {
            return null;
        }

        var trainerName = await db.Trainers.Where(trainer => trainer.Id == trainerId).Select(trainer => trainer.FullName).SingleAsync(cancellationToken);

        return Notification.ForSessionCancelled(
            memberId,
            changeId,
            member.PhoneNumber,
            SmsText.ForSessionCancelled(member.FullName, trainerName, nearest.Date, nearest.Start, stillCancelled.Count - 1));
    }

    /// <summary>Saves the row before its request. <c>false</c> when this change's is already there.</summary>
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

    /// <summary>A failure that may pass is tried again, as for the trainer's SMS, while the tries and the hours last.</summary>
    private async Task<SessionCancelledSmsOutcome> SendWithRetriesAsync(Notification notification, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        while (outcome == SmsSendOutcome.RetryableFailure)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), Kind) is not { } delay)
            {
                notification.GiveUp();
                await db.SaveChangesAsync(CancellationToken.None);
                return SessionCancelledSmsOutcome.Failed;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;
            outcome = await SmsRequest.SendAsync(db, sender, time, notification);
        }

        return outcome switch
        {
            SmsSendOutcome.Sent => SessionCancelledSmsOutcome.Sent,
            SmsSendOutcome.Unknown => SessionCancelledSmsOutcome.Unknown,
            _ => SessionCancelledSmsOutcome.Failed,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cancelled session's SMS for change {ChangeId} and member {MemberId}: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid changeId, Guid memberId, SessionCancelledSmsOutcome outcome);
}

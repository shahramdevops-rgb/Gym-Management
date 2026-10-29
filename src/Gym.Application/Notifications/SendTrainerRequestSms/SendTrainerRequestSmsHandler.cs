using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Coaching;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendTrainerRequestSms;

/// <summary>
/// Sends one trainer's SMS about a member's request (BUSINESS_RULES.md §15 <i>A member and their
/// trainer</i>, task 15.4c), asked for by <see cref="TrainerRequestSms"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is checked again here.</b> The job may run hours after the request (one made at
/// 23:00 goes at 10:00), and by then the Owner may have turned the SMS off, the member may have taken
/// the request back or picked someone else, or the trainer may have answered it in the app. Only a
/// request still waiting is worth an SMS (proposed by Claude, 1405/08/07).
/// </para>
/// <para>
/// <b>Written, then sent, like the welcome.</b> The row is saved <c>Pending</c> before the request,
/// and the unique index on member, trainer and day refuses a second one: a member asking the same
/// trainer again that day reaches them in the app only.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one at a time.
/// </para>
/// </remarks>
public sealed partial class SendTrainerRequestSmsHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    TrainerRequestSms trainerRequestSms,
    ILogger<SendTrainerRequestSmsHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.TrainerRequest;

    public async Task<TrainerRequestSmsOutcome> Handle(Guid requestId, CancellationToken cancellationToken)
    {
        var outcome = await SendAsync(requestId, cancellationToken);
        LogOutcome(logger, requestId, outcome);

        return outcome;
    }

    private async Task<TrainerRequestSmsOutcome> SendAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.TrainerRequests.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TrainerRequestSmsOutcome.NoLongerDue;
        }

        // These jobs run one at a time, so a row found Pending was left by a job that stopped.
        var written = await db.Notifications
            .SingleOrDefaultAsync(
                notification => notification.Kind == Kind
                    && notification.MemberId == request.MemberId
                    && notification.TrainerId == request.TrainerId
                    && notification.ReportDate == request.RequestedOn,
                cancellationToken);
        if (written is not null)
        {
            if (written.MarkInterrupted().IsFailure)
            {
                return TrainerRequestSmsOutcome.AlreadyWritten;
            }

            await db.SaveChangesAsync(cancellationToken);
            return TrainerRequestSmsOutcome.Interrupted;
        }

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.TrainerRequestEnabled)
        {
            return TrainerRequestSmsOutcome.Off;
        }

        // A job whose time came while the server was down may run at night: it waits for 10:00.
        if (!SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay()))
        {
            trainerRequestSms.Queue(requestId);
            return TrainerRequestSmsOutcome.Deferred;
        }

        if (await BuildAsync(request, cancellationToken) is not { } notification)
        {
            return TrainerRequestSmsOutcome.NoLongerDue;
        }

        if (!await TryWriteAsync(notification, cancellationToken))
        {
            return TrainerRequestSmsOutcome.AlreadyWritten;
        }

        return await SendWithRetriesAsync(notification, cancellationToken);
    }

    /// <summary>The message, or <c>null</c> when it is no longer due.</summary>
    private async Task<Notification?> BuildAsync(TrainerRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsPending)
        {
            return null;
        }

        var trainer = await db.Trainers.AsNoTracking().Include(row => row.Periods)
            .SingleAsync(row => row.Id == request.TrainerId, cancellationToken);
        if (!trainer.IsActive)
        {
            return null;
        }

        var member = await db.Members.AsNoTracking()
            .Where(row => row.Id == request.MemberId)
            .Select(row => new { row.FullName, row.IsActive })
            .SingleAsync(cancellationToken);
        if (!member.IsActive)
        {
            return null;
        }

        return Notification.ForTrainerRequest(
            request.MemberId,
            request.TrainerId,
            request.RequestedOn,
            trainer.PhoneNumber,
            SmsText.ForTrainerRequest(trainer.FullName, member.FullName));
    }

    /// <summary>Saves the row before its request. <c>false</c> when today's is already there.</summary>
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

    /// <summary>A failure that may pass is tried again, as for the welcome, while the tries and the hours last.</summary>
    private async Task<TrainerRequestSmsOutcome> SendWithRetriesAsync(Notification notification, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        while (outcome == SmsSendOutcome.RetryableFailure)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), Kind) is not { } delay)
            {
                notification.GiveUp();
                await db.SaveChangesAsync(CancellationToken.None);
                return TrainerRequestSmsOutcome.Failed;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;
            outcome = await SmsRequest.SendAsync(db, sender, time, notification);
        }

        return outcome switch
        {
            SmsSendOutcome.Sent => TrainerRequestSmsOutcome.Sent,
            SmsSendOutcome.Unknown => TrainerRequestSmsOutcome.Unknown,
            _ => TrainerRequestSmsOutcome.Failed,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trainer's SMS for request {RequestId}: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid requestId, TrainerRequestSmsOutcome outcome);
}

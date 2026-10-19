using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendWelcome;

/// <summary>
/// Sends one member's welcome (BUSINESS_RULES.md §10 <i>The welcome</i>, task 10.10), asked for by
/// <see cref="Welcomes"/> when their first plan was sold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is checked again here.</b> The job may run hours after the sale (a sale at 23:00 is
/// welcomed at 10:00), and by then the Owner may have turned the welcome off, the plan may have been
/// cancelled, or the member deactivated.
/// </para>
/// <para>
/// <b>Written, then sent, like the receipt.</b> The row is saved <c>Pending</c> before the request,
/// and the unique index on the member for this kind refuses a second one. If the app stops between
/// the two saves, Hangfire makes the job again once the app is back; it then finds the row still
/// <c>Pending</c> and marks it <c>Unknown</c>, since the request may have left.
/// </para>
/// <para>
/// No HTTP endpoint calls this; Gym.Infrastructure/Jobs runs it with Hangfire, one welcome at a time.
/// </para>
/// </remarks>
public sealed partial class SendWelcomeHandler(
    IAppDbContext db,
    IGymCalendar calendar,
    TimeProvider time,
    ISmsSender sender,
    SmsRetrySchedule retries,
    Welcomes welcomes,
    ILogger<SendWelcomeHandler> logger)
{
    private const NotificationKind Kind = NotificationKind.Welcome;

    /// <param name="subscriptionId">The plan that was the member's first when it was sold.</param>
    public async Task<WelcomeOutcome> Handle(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var outcome = await SendAsync(subscriptionId, cancellationToken);
        LogOutcome(logger, subscriptionId, outcome);

        return outcome;
    }

    private async Task<WelcomeOutcome> SendAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var memberId = await db.Subscriptions.AsNoTracking()
            .Where(subscription => subscription.Id == subscriptionId)
            .Select(subscription => (Guid?)subscription.MemberId)
            .SingleOrDefaultAsync(cancellationToken);
        if (memberId is null)
        {
            return WelcomeOutcome.NoLongerDue;
        }

        // Welcomes run one at a time, so this plan's row found Pending was left by a job that stopped.
        // A row for another of the member's plans means they were welcomed already (§10: once per member).
        var written = await db.Notifications
            .SingleOrDefaultAsync(
                notification => notification.MemberId == memberId && notification.Kind == Kind,
                cancellationToken);
        if (written is not null)
        {
            if (written.SubscriptionId != subscriptionId || written.MarkInterrupted().IsFailure)
            {
                return WelcomeOutcome.AlreadyWritten;
            }

            await db.SaveChangesAsync(cancellationToken);
            return WelcomeOutcome.Interrupted;
        }

        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.WelcomeEnabled)
        {
            return WelcomeOutcome.Off;
        }

        // A job whose time came while the server was down may run at night: it waits for 10:00.
        if (!SmsSettings.IsWithinSendingHours(Kind, calendar.TimeOfDay()))
        {
            welcomes.Queue(subscriptionId);
            return WelcomeOutcome.Deferred;
        }

        if (await BuildAsync(subscriptionId, memberId.Value, cancellationToken) is not { } notification)
        {
            return WelcomeOutcome.NoLongerDue;
        }

        if (!await TryWriteAsync(notification, cancellationToken))
        {
            return WelcomeOutcome.AlreadyWritten;
        }

        return await SendWithRetriesAsync(notification, cancellationToken);
    }

    /// <summary>The message, or <c>null</c> when the welcome is no longer due.</summary>
    private async Task<Notification?> BuildAsync(Guid subscriptionId, Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking()
            .Where(member => member.Id == memberId)
            .Select(member => new { member.FullName, member.PhoneNumber, member.IsActive })
            .SingleAsync(cancellationToken);
        if (!member.IsActive)
        {
            return null;
        }

        // Cancelled since the sale: no longer a plan, so no longer the first (§10).
        if (!await Welcomes.IsFirstPlanAsync(db, subscriptionId, cancellationToken))
        {
            return null;
        }

        return Notification.ForWelcome(memberId, subscriptionId, member.PhoneNumber, SmsText.ForWelcome(member.FullName));
    }

    /// <summary>Saves the row before its request. <c>false</c> when the member already has one.</summary>
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
    private async Task<WelcomeOutcome> SendWithRetriesAsync(Notification notification, CancellationToken cancellationToken)
    {
        var attemptsMade = 1;
        var outcome = await SmsRequest.SendAsync(db, sender, time, notification);

        while (outcome == SmsSendOutcome.RetryableFailure)
        {
            if (retries.NextDelay(attemptsMade, calendar.TimeOfDay(), Kind) is not { } delay)
            {
                notification.GiveUp();
                await db.SaveChangesAsync(CancellationToken.None);
                return WelcomeOutcome.Failed;
            }

            await Task.Delay(delay, time, cancellationToken);
            attemptsMade++;
            outcome = await SmsRequest.SendAsync(db, sender, time, notification);
        }

        return outcome switch
        {
            SmsSendOutcome.Sent => WelcomeOutcome.Sent,
            SmsSendOutcome.Unknown => WelcomeOutcome.Unknown,
            _ => WelcomeOutcome.Failed,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Welcome for subscription {SubscriptionId}: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid subscriptionId, WelcomeOutcome outcome);
}

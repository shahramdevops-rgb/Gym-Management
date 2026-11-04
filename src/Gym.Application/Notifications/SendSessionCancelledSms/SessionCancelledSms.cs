using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Timetables;
using Gym.Domain.Notifications;

using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendSessionCancelledSms;

/// <summary>
/// Asks for the students' SMS when their trainer cancels sessions they are in (BUSINESS_RULES.md §15
/// <i>Timetable and attendance</i>, task 15.6c): at once within 08:00–22:00, otherwise at 08:00. Called
/// by the timetable's save handlers after their transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>After the commit, never inside it</b>, as for the trainer's SMS: the job must find the rows.
/// </para>
/// <para>
/// <b>An SMS never fails a save.</b> The change is saved and the students see it in the app; if the
/// job runner cannot take an SMS, that one is lost and logged.
/// </para>
/// </remarks>
public sealed partial class SessionCancelledSms(
    IAppDbContext db,
    IGymCalendar calendar,
    ISessionCancelledSmsScheduler scheduler,
    ILogger<SessionCancelledSms> logger)
{
    internal async Task QueueAsync(CancelledSessions cancelled, CancellationToken cancellationToken)
    {
        if (cancelled.SmsTo.Count == 0)
        {
            return;
        }

        // §10: only while it is on at the save; turning it on later reaches no one.
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.SessionCancelledEnabled)
        {
            return;
        }

        foreach (var memberId in cancelled.SmsTo)
        {
            try
            {
                Queue(cancelled.ChangeId, memberId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogNotQueued(logger, exception, cancelled.ChangeId, memberId);
            }
        }
    }

    /// <summary>
    /// Now, or at 08:00 when it is outside the sending hours. The job calls it too, for an SMS whose
    /// time came while the server was down and is now outside the hours.
    /// </summary>
    public void Queue(Guid changeId, Guid memberId)
    {
        if (SmsSettings.DeferredToOpening(calendar.Today(), calendar.TimeOfDay()) is { } morning)
        {
            var at = TimeZoneInfo.ConvertTimeToUtc(morning, calendar.TimeZone);
            scheduler.SendAt(changeId, memberId, new DateTimeOffset(at, TimeSpan.Zero));
        }
        else
        {
            scheduler.SendNow(changeId, memberId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The cancelled session's SMS for change {ChangeId} and member {MemberId} could not be queued; it will not be sent")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid changeId, Guid memberId);
}

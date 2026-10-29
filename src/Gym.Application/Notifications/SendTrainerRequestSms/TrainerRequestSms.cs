using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendTrainerRequestSms;

/// <summary>
/// Asks for the trainer's SMS when a member picks them on the site (BUSINESS_RULES.md §15 <i>A
/// member and their trainer</i>, task 15.4c): at once within 08:00–22:00, otherwise at 10:00 the next
/// morning. Called by <c>RequestTrainerHandler</c> after its transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>After the commit, never inside it</b>, as for the welcome: the job must find the request.
/// </para>
/// <para>
/// <b>An SMS never fails a request.</b> The request is saved and the trainer sees it in the app; if
/// the job runner cannot take the SMS, that one is lost and logged.
/// </para>
/// </remarks>
public sealed partial class TrainerRequestSms(
    IAppDbContext db,
    IGymCalendar calendar,
    ITrainerRequestSmsScheduler scheduler,
    ILogger<TrainerRequestSms> logger)
{
    public async Task QueueAsync(Guid requestId, CancellationToken cancellationToken)
    {
        // §10: only while it is on when the request is made; turning it on later reaches no one.
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.TrainerRequestEnabled)
        {
            return;
        }

        try
        {
            Queue(requestId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotQueued(logger, exception, requestId);
        }
    }

    /// <summary>
    /// Now, or at 10:00 when it is outside the sending hours. The job calls it too, for an SMS whose
    /// time came while the server was down and is now outside the hours.
    /// </summary>
    public void Queue(Guid requestId)
    {
        if (SmsSettings.DeferredToMorning(calendar.Today(), calendar.TimeOfDay()) is { } morning)
        {
            var at = TimeZoneInfo.ConvertTimeToUtc(morning, calendar.TimeZone);
            scheduler.SendAt(requestId, new DateTimeOffset(at, TimeSpan.Zero));
        }
        else
        {
            scheduler.SendNow(requestId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The trainer's SMS for request {RequestId} could not be queued; it will not be sent")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid requestId);
}

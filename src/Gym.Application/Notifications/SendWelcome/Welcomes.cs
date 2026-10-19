using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Application.Notifications.SendWelcome;

/// <summary>
/// Asks for a member's welcome when their first plan is sold (BUSINESS_RULES.md §10 <i>The
/// welcome</i>, task 10.10): at once within 08:00–22:00, otherwise at 10:00 the next morning. Called
/// by every sale of a plan (assign, renew, a plan sold with a check-in), after its transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>After the commit, never inside it.</b> The job must find the plan when it runs, and a sale
/// rolled back must not leave a welcome behind.
/// </para>
/// <para>
/// <b>A welcome never fails a sale.</b> The plan is already saved when this runs. If the job runner
/// cannot take the welcome, that one SMS is lost and logged, as a receipt would be.
/// </para>
/// </remarks>
public sealed partial class Welcomes(
    IAppDbContext db,
    IGymCalendar calendar,
    IWelcomeScheduler scheduler,
    ILogger<Welcomes> logger)
{
    public async Task QueueIfFirstPlanAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        // §10: only while it is on when the sale is written; turning it on later reaches no one.
        var settings = await SmsSettingsRow.CurrentAsync(db, cancellationToken);
        if (!settings.Enabled || !settings.WelcomeEnabled)
        {
            return;
        }

        if (!await IsFirstPlanAsync(db, subscriptionId, cancellationToken))
        {
            return;
        }

        try
        {
            Queue(subscriptionId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotQueued(logger, exception, subscriptionId);
        }
    }

    /// <summary>
    /// Now, or at 10:00 when it is outside the sending hours. The welcome job calls it too, for a
    /// welcome whose time came while the server was down and is now outside the hours.
    /// </summary>
    public void Queue(Guid subscriptionId)
    {
        if (SmsSettings.DeferredToMorning(calendar.Today(), calendar.TimeOfDay()) is { } morning)
        {
            var at = TimeZoneInfo.ConvertTimeToUtc(morning, calendar.TimeZone);
            scheduler.SendAt(subscriptionId, new DateTimeOffset(at, TimeSpan.Zero));
        }
        else
        {
            scheduler.SendNow(subscriptionId);
        }
    }

    /// <summary>
    /// Whether the plan is the member's first (<see cref="SmsAudience.IsFirstPlan"/>), read from the
    /// member's plans as they stand now. <c>false</c> for a plan that does not exist.
    /// </summary>
    internal static async Task<bool> IsFirstPlanAsync(IAppDbContext db, Guid subscriptionId, CancellationToken cancellationToken)
    {
        var plan = await db.Subscriptions.AsNoTracking()
            .SingleOrDefaultAsync(subscription => subscription.Id == subscriptionId, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        var others = await db.Subscriptions.AsNoTracking()
            .Where(other => other.MemberId == plan.MemberId
                && other.Id != plan.Id
                && !other.IsSingleSession
                && other.CancelledAt == null)
            .ToListAsync(cancellationToken);

        return SmsAudience.IsFirstPlan(plan, others);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The welcome for subscription {SubscriptionId} could not be queued; it will not be sent")]
    private static partial void LogNotQueued(ILogger logger, Exception exception, Guid subscriptionId);
}

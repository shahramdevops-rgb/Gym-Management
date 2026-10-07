using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Common;
using Gym.Application.Notifications.CheckSmsDelivery;

using Hangfire;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Infrastructure.Jobs;

/// <summary>
/// Schedules the recurring jobs. Called once from <c>Program.cs</c> after the host is built, the
/// same place <c>IdentitySeeder.SeedOwnerAsync</c> runs, because both need a built
/// <see cref="IServiceProvider"/> to resolve scoped configuration.
/// </summary>
public static class RecurringJobScheduler
{
    public const string AutoCheckoutJobId = "attendance-auto-checkout";

    public const string SmsDeliveryCheckJobId = "notifications-delivery-check";

    /// <summary>
    /// When the nightly delivery check runs, in <c>Gym:TimeZone</c> (BUSINESS_RULES.md §10 <i>Sending</i>):
    /// after the last send of the day (22:00), and every 24 hours, inside the provider's 48.
    /// </summary>
    public static readonly TimeOnly SmsDeliveryCheckTime = new(23, 30);

    public static void ScheduleRecurringJobs(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        var attendancePolicy = scope.ServiceProvider.GetRequiredService<IAttendancePolicy>();
        var calendar = scope.ServiceProvider.GetRequiredService<IGymCalendar>();

        // Gym:ClosingTime as a cron "minute hour * * *", scheduled in Gym:TimeZone so it fires
        // at that local time regardless of the server's own clock or DST (BUSINESS_RULES.md §0, §7).
        var closingTime = attendancePolicy.ClosingTime;
        var cronExpression = $"{closingTime.Minute} {closingTime.Hour} * * *";

        recurringJobs.AddOrUpdate<AutoCheckoutHandler>(
            AutoCheckoutJobId,
            handler => handler.Handle(CancellationToken.None),
            cronExpression,
            new RecurringJobOptions { TimeZone = calendar.TimeZone });

        // Every night, whatever the SMS settings: with nothing sent in the last 48 hours it asks nothing.
        recurringJobs.AddOrUpdate<CheckSmsDeliveryHandler>(
            SmsDeliveryCheckJobId,
            handler => handler.Handle(CancellationToken.None),
            $"{SmsDeliveryCheckTime.Minute} {SmsDeliveryCheckTime.Hour} * * *",
            new RecurringJobOptions { TimeZone = calendar.TimeZone });
    }
}

using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Common;

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
    }
}

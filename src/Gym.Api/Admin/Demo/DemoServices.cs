using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Infrastructure.Sms;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// The services <c>admin seed-demo</c> runs with instead of the app's own: its clock, its acting
/// user, the fake SMS sender, and a receipt queue it empties itself.
/// </summary>
/// <remarks>
/// Swapped in <c>Program.cs</c> before the host is built, and only for this one command, so the
/// handlers the seeder calls are the real ones with nothing else changed. The SMS sender is the
/// fake one whatever <c>Sms:Provider</c> says: a demo must never reach a real phone or spend the
/// gym's credit, even on a developer machine set up for Kavenegar.
/// </remarks>
public static class DemoServices
{
    public static bool IsDemoSeedCommand(string[] args) => args is [AdminConsole.Command, DemoSeeder.Command];

    public static IServiceCollection AddDemoSeedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<TimeProvider>();
        services.AddSingleton(new DemoClock(TimeProvider.System));
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<DemoClock>());

        services.RemoveAll<ICurrentUser>();
        services.AddSingleton<DemoActor>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<DemoActor>());

        services.RemoveAll<ISmsSender>();
        services.RemoveAll<ISmsAccount>();
        services.AddScoped<ISmsSender>(provider => provider.GetRequiredService<FakeSmsSender>());
        services.AddScoped<ISmsAccount>(provider => provider.GetRequiredService<FakeSmsSender>());

        services.RemoveAll<ISubscriptionReceiptScheduler>();
        services.AddSingleton<DemoReceiptScheduler>();
        services.AddSingleton<ISubscriptionReceiptScheduler>(provider => provider.GetRequiredService<DemoReceiptScheduler>());

        return services;
    }
}

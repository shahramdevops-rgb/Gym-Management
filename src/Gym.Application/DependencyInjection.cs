using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gym.Application;

/// <summary>
/// The one entry point Gym.Api uses to wire up the use-case layer, so <c>Program.cs</c> names
/// layers rather than individual services.
/// </summary>
/// <remarks>
/// Deliberately thin today: handlers are plain injected classes and the first of them arrives
/// in Phase 2, with FluentValidation joining in task 0.5. The seam exists now so that adding a
/// use case later means editing this layer, never the composition root.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use cases need a clock, because CLAUDE.md forbids DateTime.UtcNow: business dates
        // such as "is this subscription expired today" must be steerable from a test.
        // AddInfrastructure registers the same singleton for the audit interceptor, so both
        // use TryAdd: each layer declares the dependency it actually has, and whichever runs
        // first wins without the other throwing or silently replacing it.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}

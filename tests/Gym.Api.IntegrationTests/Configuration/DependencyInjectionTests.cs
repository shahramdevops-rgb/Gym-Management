using Gym.Application;
using Gym.Application.Common;
using Gym.Infrastructure;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Configuration;

/// <summary>
/// A smoke test of the composition root. Registering a service and resolving it are separate
/// events, so a missing or mis-scoped registration only shows up on the first request that
/// needs it — in production, at the worst moment. Building the provider here moves that
/// discovery to the build.
/// </summary>
public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=gym;Username=gym",
                ["Postgres:Password"] = "unused-by-these-tests",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // validateScopes catches a singleton capturing a scoped service, which is the DI bug
        // that turns into a cross-request data leak rather than an exception.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    [Fact]
    public void AddApplicationAndAddInfrastructure_WhenRegistered_ResolveTheDatabaseContract()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAppDbContext>().ShouldNotBeNull();
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_ResolvesTheSameInstanceForBothContextViews()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var contract = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var concrete = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Two views of one context. If these were separate instances they would have separate
        // change trackers, so a handler writing through IAppDbContext and something else
        // reading through AppDbContext would silently disagree, and saves would land in
        // different transactions.
        contract.ShouldBeSameAs(concrete);
    }

    [Fact]
    public void AddInfrastructure_WhenRegistered_ResolvesTheIdentityManagers()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<UserManager<User>>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddApplicationAndAddInfrastructure_WhenBothRegisterTheClock_ResolveOneInstance()
    {
        using var provider = BuildProvider();

        // Both layers declare the dependency with TryAddSingleton, so each works alone and
        // registering both does not produce two clocks that could disagree.
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddInfrastructure_WhenNoPasswordIsConfigured_ThrowsAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=gym;Username=gym",
            })
            .Build();

        // Failing here beats failing on the first query with a bare authentication error that
        // looks like the database is down.
        var exception = Should.Throw<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(configuration));

        exception.Message.ShouldContain("user-secrets");
    }

    [Fact]
    public void AddInfrastructure_WhenNoConnectionStringIsConfigured_ThrowsAtStartup()
    {
        var configuration = new ConfigurationBuilder().Build();

        Should.Throw<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(configuration));
    }
}

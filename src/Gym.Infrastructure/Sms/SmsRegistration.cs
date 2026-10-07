using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// Which sender and account the app uses (BUSINESS_RULES.md §10), in one place so the tests can build
/// exactly what the app builds.
/// </summary>
public static class SmsRegistration
{
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>Sms:Provider</c> = <c>Fake</c> (the default): <see cref="FakeSmsSender"/> for everything.</item>
    /// <item><c>Kavenegar</c> in Production: <see cref="KavenegarSmsSender"/> for everything.</item>
    /// <item><c>Kavenegar</c> anywhere else: questions go to Kavenegar, but each message goes through
    /// <see cref="AllowListSmsSender"/>, so only the numbers in <c>Sms:AllowedReceptors</c> are really sent.</item>
    /// </list>
    /// An unknown provider, or Kavenegar with no key, stops the app at startup rather than leaving the
    /// Owner's messages unsent without a word.
    /// </remarks>
    public static IServiceCollection AddSms(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>();

        // A singleton, so the fake message ids keep counting up for as long as the app runs.
        services.AddSingleton<FakeSmsSender>();

        // RemoveAllLoggers: the factory's own loggers write each request's address, and Kavenegar's
        // address holds the API key.
        services.AddHttpClient<KavenegarSmsSender>(client =>
            {
                client.BaseAddress = KavenegarSmsSender.BaseAddress;
                client.Timeout = KavenegarSmsSender.Timeout;
            })
            .RemoveAllLoggers();

        services.AddScoped(Sender);
        services.AddScoped(Account);

        // Sms:MaxAttempts and Sms:RetryDelays as the domain's schedule (BUSINESS_RULES.md §0).
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;

            return new SmsRetrySchedule(options.MaxAttempts, options.RetryDelays);
        });

        return services;
    }

    private static ISmsSender Sender(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
        var fake = provider.GetRequiredService<FakeSmsSender>();
        if (options.Provider != SmsProviders.Kavenegar)
        {
            return fake;
        }

        var kavenegar = provider.GetRequiredService<KavenegarSmsSender>();
        if (provider.GetRequiredService<IHostEnvironment>().IsProduction())
        {
            return kavenegar;
        }

        return new AllowListSmsSender(
            kavenegar,
            fake,
            options.AllowedReceptors,
            provider.GetRequiredService<ILogger<AllowListSmsSender>>());
    }

    private static ISmsAccount Account(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<SmsOptions>>().Value.Provider == SmsProviders.Kavenegar
            ? provider.GetRequiredService<KavenegarSmsSender>()
            : provider.GetRequiredService<FakeSmsSender>();
}

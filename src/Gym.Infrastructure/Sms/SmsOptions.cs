using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// The <c>Sms</c> configuration section (BUSINESS_RULES.md §10). Only what is not the Owner's to
/// choose lives here; the numbers, send times and templates are on the SMS settings page (task 10.2).
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>The most tries <see cref="MaxAttempts"/> may be set to: more would only keep a run going for nothing.</summary>
    public const int MaxAttemptsLimit = 10;

    /// <summary>
    /// Which <c>ISmsSender</c> is used. <see cref="SmsProviders.Fake"/> when left out: a missing
    /// setting must never send a message somebody pays for.
    /// </summary>
    public string Provider { get; set; } = SmsProviders.Fake;

    /// <summary>
    /// Tries in all for a message that may still pass, the first one included (§0: 3). Not on the
    /// settings page: it is about the provider, not about the gym.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// The wait before each try after the first, one fewer than <see cref="MaxAttempts"/> (§0: 1 minute,
    /// then 5). Empty by default: the configuration binder adds to a list rather than replacing it, so a
    /// default here would be doubled by appsettings.json.
    /// </summary>
    public TimeSpan[] RetryDelays { get; set; } = [];
}

/// <summary>The values <see cref="SmsOptions.Provider"/> accepts.</summary>
public static class SmsProviders
{
    /// <summary>Only logs. What tests always use, and what runs until the real provider is chosen.</summary>
    public const string Fake = "Fake";
}

/// <summary>
/// Refuses to start with a provider it does not know, instead of silently sending nothing, or with a
/// retry schedule that does not add up. The real provider joins the list in task 10.4.
/// </summary>
public sealed class SmsOptionsValidator : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Provider != SmsProviders.Fake)
        {
            return ValidateOptionsResult.Fail($"Sms:Provider '{options.Provider}' is unknown; the only provider is '{SmsProviders.Fake}'.");
        }

        if (options.MaxAttempts < 1 || options.MaxAttempts > SmsOptions.MaxAttemptsLimit)
        {
            return ValidateOptionsResult.Fail($"Sms:MaxAttempts must be from 1 to {SmsOptions.MaxAttemptsLimit}.");
        }

        if (options.RetryDelays.Length != options.MaxAttempts - 1)
        {
            return ValidateOptionsResult.Fail("Sms:RetryDelays needs one wait before each try after the first: one fewer than Sms:MaxAttempts.");
        }

        return options.RetryDelays.Any(delay => delay < TimeSpan.Zero)
            ? ValidateOptionsResult.Fail("Sms:RetryDelays cannot hold a negative wait.")
            : ValidateOptionsResult.Success;
    }
}

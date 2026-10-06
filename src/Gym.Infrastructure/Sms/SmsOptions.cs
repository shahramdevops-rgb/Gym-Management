using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// The <c>Sms</c> configuration section (BUSINESS_RULES.md §10). Only what is not the Owner's to
/// choose lives here; the numbers, send times and templates are on the SMS settings page (task 10.2).
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Which <c>ISmsSender</c> is used. <see cref="SmsProviders.Fake"/> when left out: a missing
    /// setting must never send a message somebody pays for.
    /// </summary>
    public string Provider { get; set; } = SmsProviders.Fake;
}

/// <summary>The values <see cref="SmsOptions.Provider"/> accepts.</summary>
public static class SmsProviders
{
    /// <summary>Only logs. What tests always use, and what runs until the real provider is chosen.</summary>
    public const string Fake = "Fake";
}

/// <summary>
/// Refuses to start with a provider it does not know, instead of silently sending nothing. The real
/// provider joins the list in task 10.4.
/// </summary>
public sealed class SmsOptionsValidator : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider == SmsProviders.Fake
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Sms:Provider '{options.Provider}' is unknown; the only provider is '{SmsProviders.Fake}'.");
    }
}

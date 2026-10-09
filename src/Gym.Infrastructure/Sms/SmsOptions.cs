using System.Text.RegularExpressions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Sms;

/// <summary>
/// The <c>Sms</c> configuration section (BUSINESS_RULES.md §10). Only what is not the Owner's to
/// choose lives here; the numbers and send times are on the SMS settings page (task 10.2).
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

    /// <summary>
    /// Outside Production, the only numbers the real provider may reach, in E.164 (<c>+989…</c>), as a
    /// member's number is stored (§10 <i>Development and tests</i>). Empty, the default, means nothing
    /// is really sent. Production has no such list, and refuses one.
    /// </summary>
    public string[] AllowedReceptors { get; set; } = [];

    public KavenegarOptions Kavenegar { get; set; } = new();
}

/// <summary>The <c>Sms:Kavenegar</c> section: the account's key and line. The address and the methods are Kavenegar's own.</summary>
public sealed class KavenegarOptions
{
    /// <summary>
    /// A secret: user-secrets on the developer's machine, <c>Sms__Kavenegar__ApiKey</c> on the server,
    /// never in git. Kavenegar puts it in the address of every request, so the address is never logged.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The gym's dedicated line, the number every SMS is sent from, digits only (e.g. <c>2000500666</c>).
    /// Not a secret, but it belongs to the account like the key: <c>Sms__Kavenegar__Sender</c> on the
    /// server (BUSINESS_RULES.md §10 <i>Sending</i>).
    /// </summary>
    public string Sender { get; set; } = string.Empty;
}

/// <summary>The values <see cref="SmsOptions.Provider"/> accepts.</summary>
public static class SmsProviders
{
    /// <summary>Only logs. What tests always use, and what runs until the real provider is chosen.</summary>
    public const string Fake = "Fake";

    /// <summary>Kavenegar's REST API (task 10.4).</summary>
    public const string Kavenegar = "Kavenegar";
}

/// <summary>
/// Refuses to start with a provider it does not know, instead of silently sending nothing, with the
/// real provider and no key or line, with a retry schedule that does not add up, or with a number list that
/// could not match a member's number.
/// </summary>
public sealed partial class SmsOptionsValidator(IHostEnvironment environment) : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Provider is not (SmsProviders.Fake or SmsProviders.Kavenegar))
        {
            return ValidateOptionsResult.Fail(
                $"Sms:Provider '{options.Provider}' is unknown; use '{SmsProviders.Fake}' or '{SmsProviders.Kavenegar}'.");
        }

        if (options.Provider == SmsProviders.Kavenegar && string.IsNullOrWhiteSpace(options.Kavenegar.ApiKey))
        {
            return ValidateOptionsResult.Fail("Sms:Kavenegar:ApiKey is required when Sms:Provider is Kavenegar.");
        }

        if (options.Provider == SmsProviders.Kavenegar && !LineNumber().IsMatch(options.Kavenegar.Sender))
        {
            return ValidateOptionsResult.Fail(
                "Sms:Kavenegar:Sender, the dedicated line's number in digits, is required when Sms:Provider is Kavenegar.");
        }

        if (options.MaxAttempts < 1 || options.MaxAttempts > SmsOptions.MaxAttemptsLimit)
        {
            return ValidateOptionsResult.Fail($"Sms:MaxAttempts must be from 1 to {SmsOptions.MaxAttemptsLimit}.");
        }

        if (options.RetryDelays.Length != options.MaxAttempts - 1)
        {
            return ValidateOptionsResult.Fail("Sms:RetryDelays needs one wait before each try after the first: one fewer than Sms:MaxAttempts.");
        }

        if (options.RetryDelays.Any(delay => delay < TimeSpan.Zero))
        {
            return ValidateOptionsResult.Fail("Sms:RetryDelays cannot hold a negative wait.");
        }

        if (environment.IsProduction() && options.AllowedReceptors.Length > 0)
        {
            // §10: the server has no such list. One left there would quietly keep members' messages back.
            return ValidateOptionsResult.Fail("Sms:AllowedReceptors is for the developer's machine; Production has no such list.");
        }

        return options.AllowedReceptors.All(receptor => IranianMobile().IsMatch(receptor))
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Sms:AllowedReceptors holds numbers in E.164, as members' are stored: +989xxxxxxxxx.");
    }

    [GeneratedRegex(@"^\+989\d{9}$")]
    private static partial Regex IranianMobile();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex LineNumber();
}

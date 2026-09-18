using Gym.Application.Common;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Calendar;

/// <summary>The <c>Gym</c> configuration section's time zone (BUSINESS_RULES.md §0).</summary>
public sealed class GymCalendarOptions
{
    public const string SectionName = "Gym";

    /// <summary>An IANA id such as <c>Asia/Tehran</c>.</summary>
    public string TimeZone { get; set; } = string.Empty;
}

/// <summary>Refuses to start with a time zone this machine does not know.</summary>
public sealed class GymCalendarOptionsValidator : IValidateOptions<GymCalendarOptions>
{
    public ValidateOptionsResult Validate(string? name, GymCalendarOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Gym:TimeZone '{options.TimeZone}' is not a known time zone.");
    }
}

/// <summary>
/// Today's date in the gym's time zone. At 20:30 UTC it is already the next day in Tehran
/// (UTC+03:30), so the UTC date would expire subscriptions three and a half hours late.
/// </summary>
public sealed class GymCalendar(TimeProvider time, IOptions<GymCalendarOptions> options) : IGymCalendar
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _timeZone).DateTime);
}

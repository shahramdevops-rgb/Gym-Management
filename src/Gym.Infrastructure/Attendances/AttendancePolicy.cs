using Gym.Application.Common;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Attendances;

/// <summary>The <c>Gym</c> configuration section's attendance limits (BUSINESS_RULES.md §7).</summary>
public sealed class AttendancePolicyOptions
{
    public const string SectionName = "Gym";

    public int CancelCheckInWindowMinutes { get; set; }

    /// <summary>Local time the nightly auto-checkout job runs at (BUSINESS_RULES.md §0, §7).</summary>
    public TimeOnly ClosingTime { get; set; }
}

/// <summary>Refuses to start with a negative cancel window.</summary>
public sealed class AttendancePolicyOptionsValidator : IValidateOptions<AttendancePolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, AttendancePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.CancelCheckInWindowMinutes >= 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Gym:CancelCheckInWindowMinutes must not be negative.");
    }
}

public sealed class AttendancePolicy(IOptions<AttendancePolicyOptions> options) : IAttendancePolicy
{
    public int CancelWindowMinutes => options.Value.CancelCheckInWindowMinutes;

    public TimeOnly ClosingTime => options.Value.ClosingTime;
}

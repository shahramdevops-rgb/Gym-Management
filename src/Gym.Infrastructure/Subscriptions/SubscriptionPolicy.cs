using Gym.Application.Common;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Subscriptions;

/// <summary>The <c>Gym</c> configuration section's subscription limits (BUSINESS_RULES.md §4 Freeze).</summary>
public sealed class SubscriptionPolicyOptions
{
    public const string SectionName = "Gym";

    public int MaxFreezeDaysPerSubscription { get; set; }
}

/// <summary>Refuses to start with a negative freeze allowance.</summary>
public sealed class SubscriptionPolicyOptionsValidator : IValidateOptions<SubscriptionPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, SubscriptionPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxFreezeDaysPerSubscription >= 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Gym:MaxFreezeDaysPerSubscription must not be negative.");
    }
}

public sealed class SubscriptionPolicy(IOptions<SubscriptionPolicyOptions> options) : ISubscriptionPolicy
{
    public int MaxFreezeDays => options.Value.MaxFreezeDaysPerSubscription;
}

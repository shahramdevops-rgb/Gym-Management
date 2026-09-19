namespace Gym.Application.Common;

/// <summary>Business limits for subscriptions (BUSINESS_RULES.md §4 Freeze).</summary>
public interface ISubscriptionPolicy
{
    /// <summary><c>Gym:MaxFreezeDaysPerSubscription</c>.</summary>
    int MaxFreezeDays { get; }
}

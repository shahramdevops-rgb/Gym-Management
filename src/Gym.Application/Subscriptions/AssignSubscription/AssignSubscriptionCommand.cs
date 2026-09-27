namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>
/// The plan the desk builds for the member (BUSINESS_RULES.md §3). No price: the server works it out
/// from the session price, so the desk never types one.
/// </summary>
public sealed record AssignSubscriptionCommand(int DurationDays, int SessionCount);

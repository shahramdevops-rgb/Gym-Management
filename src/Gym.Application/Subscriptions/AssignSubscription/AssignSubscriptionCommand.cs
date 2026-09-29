namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>
/// The plan the desk builds for the member (BUSINESS_RULES.md §3): only its sessions. No days and no
/// price: the server works both out from the sessions, so the desk never types either.
/// </summary>
public sealed record AssignSubscriptionCommand(int SessionCount);

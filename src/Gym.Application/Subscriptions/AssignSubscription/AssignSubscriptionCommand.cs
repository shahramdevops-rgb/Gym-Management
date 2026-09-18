namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>The member comes from the route: <c>POST /api/members/{memberId}/subscriptions</c>.</summary>
public sealed record AssignSubscriptionCommand(Guid PlanId);

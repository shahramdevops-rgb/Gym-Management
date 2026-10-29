namespace Gym.Application.SiteRecords.GetMyPlan;

/// <summary>
/// The member's plan at a glance (BUSINESS_RULES.md §15 <i>The member's own records</i>).
/// </summary>
/// <param name="Current">
/// The plan the member list shows the member's sessions from
/// (<see cref="Domain.Subscriptions.SubscriptionSchedule.CurrentPlan"/>): the active one, else the
/// frozen one, else the soonest bought for later, else the one that ended last. <c>null</c> for a
/// member who never had one.
/// </param>
/// <param name="Next">The soonest plan bought for later after <paramref name="Current"/>, or <c>null</c>.</param>
public sealed record MyPlanResponse(SitePlanResponse? Current, SitePlanResponse? Next);

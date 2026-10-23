namespace Gym.Domain.Notifications;

/// <summary>Who a broadcast goes to, chosen by the Owner each time (BUSINESS_RULES.md §10 <i>The broadcast</i>).</summary>
public enum BroadcastAudience
{
    /// <summary>«همه اعضای فعال»: every active member.</summary>
    ActiveMembers,

    /// <summary>«اعضای دارای پلن»: every active member with a plan usable today (§4 <c>Active</c>).</summary>
    MembersWithUsablePlan,
}

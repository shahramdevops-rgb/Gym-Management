namespace Gym.Application.Notifications.Broadcasts.GetBroadcastAudience;

/// <summary>Both groups the Owner can choose from, each as it stands now.</summary>
public sealed record BroadcastAudiencesResponse(BroadcastGroupResponse ActiveMembers, BroadcastGroupResponse MembersWithUsablePlan);

/// <param name="Recipients">How many members are in the group now.</param>
/// <param name="LongestFirstName">
/// The longest first name among them, empty for none: the page counts the characters and parts as
/// the Owner types with it, the way the send will check them (§10 <i>The broadcast</i>).
/// </param>
public sealed record BroadcastGroupResponse(int Recipients, string LongestFirstName);

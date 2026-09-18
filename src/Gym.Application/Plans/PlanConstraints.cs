namespace Gym.Application.Plans;

/// <summary>
/// Database index names the plan handlers react to, shared with the configuration that creates
/// them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class PlanConstraints
{
    public const string UniqueName = "ix_plans_normalized_name";
}

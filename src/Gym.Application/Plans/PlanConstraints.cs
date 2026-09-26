namespace Gym.Application.Plans;

/// <summary>
/// Database index names the plan handlers react to, shared with the configuration that creates
/// them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class PlanConstraints
{
    public const string UniqueName = "ix_plans_normalized_name";

    /// <summary>
    /// BUSINESS_RULES.md §3: at most one single-session plan. A partial unique index, so the rule is
    /// true in the database and not only in the handler that checks it first.
    /// </summary>
    public const string UniqueSingleSession = "ux_plans_single_session";
}

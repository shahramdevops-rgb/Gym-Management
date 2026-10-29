namespace Gym.Application.Coaching;

/// <summary>
/// Database index names the coaching handlers react to, named once here and used by the
/// configurations that create them, as <c>TrainerConstraints</c> does.
/// </summary>
public static class CoachingConstraints
{
    /// <summary>A member has one request waiting at most (BUSINESS_RULES.md §15).</summary>
    public const string OnePendingRequest = "ix_trainer_requests_member_id_pending";

    /// <summary>A member has one trainer at a time: one open link at most.</summary>
    public const string OneOpenLink = "ix_trainer_links_member_id_open";
}

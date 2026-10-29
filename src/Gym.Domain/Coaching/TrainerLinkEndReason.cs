namespace Gym.Domain.Coaching;

/// <summary>Why a member's link to a trainer ended (BUSINESS_RULES.md §15 <i>A member and their trainer</i>).</summary>
public enum TrainerLinkEndReason
{
    /// <summary>The member ended it on the site, with no new trainer.</summary>
    ByMember,

    /// <summary>The trainer ended it on the site.</summary>
    ByTrainer,

    /// <summary>Another trainer accepted the member's request.</summary>
    NewTrainer,

    /// <summary>The Owner stopped the trainer in the panel.</summary>
    TrainerStopped,
}

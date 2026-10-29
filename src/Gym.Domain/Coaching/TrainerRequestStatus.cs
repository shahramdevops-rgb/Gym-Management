namespace Gym.Domain.Coaching;

/// <summary>Where a member's request for a trainer stands (BUSINESS_RULES.md §15 <i>A member and their trainer</i>).</summary>
public enum TrainerRequestStatus
{
    /// <summary>Waiting for the trainer's answer. A member has one at most.</summary>
    Pending,

    /// <summary>The trainer accepted: a link was made from it.</summary>
    Accepted,

    /// <summary>The trainer declined.</summary>
    Declined,

    /// <summary>The member took it back.</summary>
    Cancelled,

    /// <summary>The member picked another trainer while it was waiting.</summary>
    Replaced,

    /// <summary>The Owner stopped the trainer while it was waiting.</summary>
    TrainerStopped,
}

namespace Gym.Domain.Coaching;

/// <summary>
/// What a member and a trainer are told in the app about each other (BUSINESS_RULES.md §15 <i>A
/// member and their trainer</i>, task 15.4d): a card with «دیدم» on the home, no SMS.
/// </summary>
/// <remarks>
/// <para>
/// The member hears that the trainer accepted or declined their request, and that the trainer ended
/// the link. The trainer hears that a student ended it. Nobody is told when another trainer takes
/// the member over, nor when the Owner stops a trainer (decided by the developer, 1405/08/19).
/// </para>
/// <para>
/// A piece of news stays until «دیدم», for <see cref="ShownForDays"/> days at most.
/// </para>
/// </remarks>
public static class CoachingNews
{
    /// <summary>
    /// Days a piece of news is shown, its own day included, if «دیدم» is never pressed (proposed by
    /// Claude, accepted by the developer 1405/08/19).
    /// </summary>
    public const int ShownForDays = 30;

    /// <summary>The earliest day whose news is still shown on <paramref name="today"/>.</summary>
    public static DateOnly FirstShownDay(DateOnly today) => today.AddDays(-(ShownForDays - 1));

    /// <summary>Whether news of <paramref name="day"/> is still shown on <paramref name="today"/>.</summary>
    public static bool IsShown(DateOnly day, DateOnly today) => day >= FirstShownDay(today);

    /// <summary>The member is told of an end only when the trainer ended it.</summary>
    public static bool TellsMember(TrainerLinkEndReason reason) => reason == TrainerLinkEndReason.ByTrainer;

    /// <summary>The trainer is told of an end only when the student ended it.</summary>
    public static bool TellsTrainer(TrainerLinkEndReason reason) => reason == TrainerLinkEndReason.ByMember;
}

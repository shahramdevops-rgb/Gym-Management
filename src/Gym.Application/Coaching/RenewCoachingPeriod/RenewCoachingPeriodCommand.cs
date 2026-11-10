namespace Gym.Application.Coaching.RenewCoachingPeriod;

/// <summary>
/// «تمدید»: <paramref name="Days"/> more days of coaching after the period now; for a link made
/// before 15.7h, the days left from today.
/// </summary>
public sealed record RenewCoachingPeriodCommand(int Days);

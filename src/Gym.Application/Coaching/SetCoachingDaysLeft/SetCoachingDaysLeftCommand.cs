namespace Gym.Application.Coaching.SetCoachingDaysLeft;

/// <summary>
/// «ویرایش دوره» (15.7k): the coaching runs <paramref name="DaysLeft"/> days from today, today
/// counted, so its last day moves earlier or later.
/// </summary>
public sealed record SetCoachingDaysLeftCommand(int DaysLeft);

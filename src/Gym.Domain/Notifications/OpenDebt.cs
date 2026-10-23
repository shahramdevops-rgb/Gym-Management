namespace Gym.Domain.Notifications;

/// <summary>
/// What a member owes right now, as the debt reminder sees it (BUSINESS_RULES.md §10 <i>The debt
/// reminder</i>, task 10.11): the whole amount, and when the oldest sale still unpaid was recorded.
/// </summary>
/// <param name="Owed">The whole open account (§5 <i>Member debt</i>), newer sales included.</param>
/// <param name="OldestSoldAt">The moment the oldest unpaid sale was recorded (UTC).</param>
/// <param name="OldestSoldOn">The gym's day of that moment: the days are counted from it.</param>
public sealed record OpenDebt(decimal Owed, DateTimeOffset OldestSoldAt, DateOnly OldestSoldOn);

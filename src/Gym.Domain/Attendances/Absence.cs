namespace Gym.Domain.Attendances;

/// <summary>
/// How long a member with a plan has been away (BUSINESS_RULES.md §12 <i>Needs attention</i>,
/// «دیگر نمی‌آید», and §10 <i>The absence SMS</i>, task 10.12). One rule, so the board and the SMS
/// never disagree about who has stopped coming.
/// </summary>
public static class Absence
{
    /// <summary>
    /// The gym's day the absence counts from: the last visit, or the plan's start when the member has
    /// not come since it started, so a plan bought yesterday is not a member who stopped coming.
    /// </summary>
    /// <param name="lastVisitOn">The gym's day of the member's last check-in not cancelled (cardio-only counts); <c>null</c> for none.</param>
    /// <param name="planStart">The start of the plan the member holds today.</param>
    public static DateOnly Since(DateOnly? lastVisitOn, DateOnly planStart) =>
        lastVisitOn is { } day && day > planStart ? day : planStart;

    /// <summary>Whole days from <see cref="Since"/> to today: 0 on the day itself.</summary>
    public static int DaysAway(DateOnly? lastVisitOn, DateOnly planStart, DateOnly today) =>
        today.DayNumber - Since(lastVisitOn, planStart).DayNumber;
}

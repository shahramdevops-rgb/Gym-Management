using Gym.Domain.Common;

namespace Gym.Domain.Coaching;

/// <summary>
/// A stretch of days a trainer coaches a student (دورهٔ مربی‌گری, BUSINESS_RULES.md §15 <i>The
/// coaching period</i>): the first one is typed when the trainer accepts the request, and each
/// «تمدید» adds another after it. Kept, never changed, so the history of renewals stays.
/// </summary>
/// <remarks>
/// <b>The link outlives it.</b> When the last period is over the student stays the trainer's, but no
/// session is placed after its last day, until the trainer renews. A link made before 15.7h has no
/// period until the trainer types the days left. Two periods of a link never share a day, held by an
/// exclusion constraint too.
/// </remarks>
public sealed class CoachingPeriod : Entity
{
    /// <summary>«One month», the number the box starts at (decided by the developer, 1405/08/19).</summary>
    public const int DefaultDays = 30;

    /// <summary>The fewest days of a period the trainer accepts or renews with.</summary>
    public const int MinDays = 7;

    /// <summary>
    /// The most days of one period: the timetable's 12 weeks (<c>TimetableRules.LastDay</c>), the
    /// furthest a session can be placed.
    /// </summary>
    public const int MaxDays = 84;

    /// <summary>
    /// The fewest days left the trainer may type for a link made before 15.7h: a student may have
    /// only a few days of their program left (proposed by Claude, accepted by the developer).
    /// </summary>
    public const int MinDaysLeft = 1;

    // For EF Core.
    private CoachingPeriod()
    {
    }

    public Guid LinkId { get; private set; }

    public DateOnly StartsOn { get; private set; }

    /// <summary>Its last day: a session on it is still in the period.</summary>
    public DateOnly EndsOn { get; private set; }

    public int Days => EndsOn.DayNumber - StartsOn.DayNumber + 1;

    internal static CoachingPeriod Of(Guid linkId, DateOnly startsOn, int days) =>
        new() { LinkId = linkId, StartsOn = startsOn, EndsOn = startsOn.AddDays(days - 1) };

    internal static bool IsAllowed(int days, int minDays) => days >= minDays && days <= MaxDays;
}
